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
    const pinned = n => n.data('root') || n.id() === f.focus;
    cy.batch(() => {
        cy.elements().removeClass('hidden focus');
        cy.nodes().filter(n => !pinned(n) && (
            f.hide.has(n.id())
            || !passes(n, f.nodes, f.except)
            || (!f.muted && n.data('muted')))).addClass('hidden');
        // A group hidden takes its members with it; a member that survived keeps its group.
        cy.nodes('.hidden').descendants().addClass('hidden');
        cy.nodes().not('.hidden').ancestors().removeClass('hidden');
        cy.edges().filter(e =>
            f.hide.has(e.id())
            || !passes(e, f.edges, f.edgesExcept)
            || e.source().hasClass('hidden') || e.target().hasClass('hidden')).addClass('hidden');
        if (!f.isolated) {
            cy.nodes().not('.hidden').filter(n => !n.isParent() && !pinned(n)
                && n.connectedEdges().not('.hidden').empty()).addClass('hidden');
            cy.nodes(':parent').not('.hidden').filter(p => p.descendants().not('.hidden').empty()).addClass('hidden');
        }
        const focus = f.focus ? cy.getElementById(f.focus) : null;
        if (focus && focus.nonempty() && !focus.hasClass('hidden')) {
            focus.addClass('focus');
            let reach = focus;
            for (let i = 0; i < f.depth; i++) reach = reach.union(reach.neighborhood().not('.hidden'));
            const keep = reach.union(reach.nodes().edgesWith(reach.nodes()).not('.hidden')).union(reach.ancestors());
            cy.elements().not(keep).addClass('hidden');
        }
    });
    if (g.selected && g.selected.hasClass('hidden')) select(g, null);
    if (g.keyed && g.keyed.hasClass('hidden')) g.keyed = null;
}

function searchText(n) {
    const d = n.data();
    return [d.label, d.id, d.meta, d.kind, ...(d.tags || [])].filter(Boolean).join(' ').toLowerCase();
}

function search(g, text, fit = true) {
    const cy = g.cy;
    const words = String(text ?? '').trim().toLowerCase().split(/\s+/).filter(Boolean);
    g.query = words.join(' ');
    cy.batch(() => {
        cy.nodes('.match').removeClass('match');
        if (words.length) {
            cy.nodes().not('.hidden').filter(n => {
                const hay = searchText(n);
                return words.every(w => hay.includes(w));
            }).addClass('match');
        }
    });
    const matches = cy.nodes('.match');
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
