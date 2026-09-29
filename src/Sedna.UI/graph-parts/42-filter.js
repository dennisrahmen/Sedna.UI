/* ── Filters, search and focus ────────────────────────────────────────────────
   Filters narrow a drawing that is already on screen; they never ask for new data.

     {
       nodes:       { kind: ['service', 'db'] },   only records with one of these values
       except:      { group: ['payments'] },       not records with one of these values
       edges:       { kind: ['calls'] },            only links …
       edgesExcept: { kind: ['mentions'] },         not links …
       hide:        ['legacy-api'],                 ids, records or links
       muted:       true,     show records marked data-muted (default: shown)
       isolated:    true,     show records with no visible link (default: shown)
       focus:       'orders-api', depth: 2          one record's neighbourhood
     }

   The two are different questions. `nodes` is a choice — "this customer" — so a record
   without the field is not what was chosen, and goes. `except` is a switch — a legend
   chip turned off — so it hides what it names and nothing else: a record in no group
   stays when a group's chip goes off. That is why chips (checkboxes, toggle buttons)
   fill `except` and a select or radios fill `nodes`.

   A field names a property (kind, group, cluster, tone, tags, …) or any other data-*
   attribute; a record matches when one of its values is listed, so `tags` matches on
   any tag. Order matters: records by their fields, then links by theirs and by whether
   both ends survived, then — if asked — the records nothing visible touches, then the
   focus.

   Search marks rather than hides: where a record sits among the others is the thing a
   graph is for. Every word typed has to appear in the record's name, id, `data-meta`,
   kind or tags.
   ─────────────────────────────────────────────────────────────────────────── */

function filterOf(spec) {
    const s = spec || {};
    const fields = o => {
        const out = {};
        for (const [k, v] of Object.entries(o || {})) {
            if (v === undefined || v === null) continue;
            out[k] = new Set((Array.isArray(v) ? v : [v]).map(String));
        }
        return out;
    };
    return {
        nodes: fields(s.nodes),
        except: fields(s.except),
        edges: fields(s.edges),
        edgesExcept: fields(s.edgesExcept),
        hide: new Set((s.hide || []).map(String)),
        muted: s.muted !== false,
        isolated: s.isolated !== false,
        focus: s.focus ? String(s.focus) : null,
        depth: Math.max(1, Math.min(Number(s.depth) || 1, 6)),
    };
}

function valuesOf(data, field) {
    const v = valueOf(data, field);
    return Array.isArray(v) ? v : v === undefined || v === null || v === '' ? [] : [v];
}

// Allowed by every "only these", and named by no "not these".
function passes(ele, only, not) {
    const data = ele.data();
    for (const [field, allowed] of Object.entries(only)) {
        if (!valuesOf(data, field).some(x => allowed.has(String(x)))) return false;
    }
    for (const [field, refused] of Object.entries(not || {})) {
        if (valuesOf(data, field).some(x => refused.has(String(x)))) return false;
    }
    return true;
}

function applyFilter(g, spec) {
    const cy = g.cy;
    const f = filterOf(spec);
    g.filter = spec || {};
    // The focus is what the reader is looking at, so a filter never takes it away. A root
    // is kept only from being hidden as unlinked — a chip that is off hides it like any record.
    const pinned = n => n.id() === f.focus;
    // Worked out in a set first, and only what changes is restyled: a chip that hides a
    // tenth of a large graph touches that tenth, not every element twice.
    const hidden = new Set();
    const shown = ele => !hidden.has(ele);
    const nodes = cy.nodes();
    nodes.forEach(n => {
        if (!pinned(n) && (f.hide.has(n.id()) || !passes(n, f.nodes, f.except) || (!f.muted && n.data('muted')))) hidden.add(n);
    });
    // A group hidden takes its members with it; a member that survived keeps its group.
    nodes.forEach(n => { if (hidden.has(n) && n.isParent()) n.descendants().forEach(d => hidden.add(d)); });
    nodes.forEach(n => { if (!hidden.has(n) && n.isChild()) n.ancestors().forEach(a => hidden.delete(a)); });
    cy.edges().forEach(e => {
        if (f.hide.has(e.id()) || !passes(e, f.edges, f.edgesExcept) || hidden.has(e.source()[0]) || hidden.has(e.target()[0])) hidden.add(e);
    });
    if (!f.isolated) {
        nodes.forEach(n => {
            if (shown(n) && !n.isParent() && !pinned(n) && !n.data('root') && !n.connectedEdges().some(shown)) hidden.add(n);
        });
        nodes.forEach(p => { if (shown(p) && p.isParent() && !p.descendants().some(shown)) hidden.add(p); });
    }
    const focus = f.focus ? cy.getElementById(f.focus) : null;
    const focused = focus && focus.nonempty() && shown(focus[0]) ? focus : cy.collection();
    if (focused.nonempty()) {
        let reach = focused;
        for (let i = 0; i < f.depth; i++) reach = reach.union(reach.neighborhood().filter(shown));
        const keep = reach.union(reach.nodes().edgesWith(reach.nodes()).filter(shown)).union(reach.ancestors());
        cy.elements().forEach(e => { if (!keep.has(e)) hidden.add(e); });
    }
    cy.batch(() => {
        cy.elements().filter(e => hidden.has(e) !== e.hasClass('hidden')).toggleClass('hidden');
        cy.nodes('.focus').not(focused).removeClass('focus');
        focused.not('.focus').addClass('focus');
    });
    if (g.selected && g.selected.hasClass('hidden')) select(g, null);
    if (g.keyed && g.keyed.hasClass('hidden')) g.keyed = null;
    if (g.tip?.showing && (g.tip.showing.removed() || g.tip.showing.hasClass('hidden'))) g.tip.hide();
}

function searchText(n) {
    const d = n.data();
    return [d.label, d.id, d.meta, d.kind, ...(d.tags || [])].filter(Boolean).join(' ').toLowerCase();
}

function search(g, text, fit = true) {
    const cy = g.cy;
    const words = String(text ?? '').trim().toLowerCase().split(/\s+/).filter(Boolean);
    g.query = words.join(' ');
    // Only what starts or stops matching is restyled: a query refined a letter at a time
    // over a large graph otherwise marks every match again on every letter.
    const matches = words.length
        ? cy.nodes().filter(n => {
            if (n.hasClass('hidden')) return false;
            const hay = searchText(n);
            return words.every(w => hay.includes(w));
        })
        : cy.collection();
    cy.batch(() => {
        cy.nodes('.match').not(matches).removeClass('match');
        matches.not('.match').addClass('match');
    });
    if (fit && matches.nonempty()) fitView(g, matches.closedNeighborhood().not('.hidden'), true);
    else declutter(g);
    return matches;
}

function stats(g) {
    const cy = g.cy;
    const shownNodes = cy.nodes().not('.hidden').filter(n => !n.hasClass('eh-ghost') && !n.hasClass('eh-handle'));
    return {
        nodes: shownNodes.length,
        edges: cy.edges().not('.hidden').filter(e => !e.hasClass('eh-preview') && !e.hasClass('eh-ghost-edge')).length,
        matches: cy.nodes('.match').length,
        totalNodes: g.model.nodes.length,
        totalEdges: g.model.edges.length,
        selected: g.selected && g.selected.nonempty() ? g.selected.id() : null,
    };
}
