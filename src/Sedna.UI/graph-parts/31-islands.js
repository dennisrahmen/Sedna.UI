/* ── Islands ──────────────────────────────────────────────────────────────────
   A large graph is read by its groups — a project and what is filed under it, a team
   and its services, a rack and its hosts — so it is laid out as islands: each
   `data-group` with its members, drawn by its own springs, and the islands packed in
   rows as wide as the frame, a `data-cluster`'s islands side by side.

   Springs alone never find that shape. A record works across groups, a link joins two
   of them, and cose pulls with the square of the distance — so the few long bridges
   pull harder than everything inside a group and the clusters melt into one ball.

   The record whose id IS a group's name is that group's heart: it is placed first, at
   the centre of its island. What joins several islands — a person in three teams, a
   shared library — is put between them, where its neighbours are.
   ─────────────────────────────────────────────────────────────────────────── */

async function islands(cy, visible, aspect, spacing) {
    const nodes = visible.nodes().filter(n => !n.isParent());
    const gap = 36 * spacing;

    // Which island a record is on: its group's — or, for one in no group, the one island
    // all its neighbours are on, asked twice so a record reached only through another
    // ungrouped record finds it too.
    const home = new Map();
    nodes.forEach(n => {
        if (n.data('group')) home.set(n.id(), n.data('group'));
    });
    if (home.size === 0) return false;
    for (let pass = 0; pass < 2; pass++) {
        nodes.filter(n => !home.has(n.id())).forEach(n => {
            const theirs = new Set(n.neighborhood().nodes().not('.hidden').map(o => home.get(o.id())).filter(Boolean));
            if (theirs.size === 1) home.set(n.id(), [...theirs][0]);
        });
    }

    const members = new Map();
    nodes.forEach(n => {
        const island = home.get(n.id());
        if (island) {
            if (!members.has(island)) members.set(island, []);
            members.get(island).push(n);
        }
    });

    // Each island on its own: seeded on a sunflower spiral, its heart at the centre,
    // then its own springs with the bridges left out. One run an island rather than one
    // over everything: cose weighs every record against every other in its run, so
    // twenty islands of thirty cost a twentieth of one run over six hundred.
    const list = [];
    const inside = cy.collection();
    for (const [id, group] of members) {
        group.sort((a, b) => (a.id() === id ? -1 : b.id() === id ? 1 : (b.data('degree') || 0) - (a.data('degree') || 0) || byId(a, b)));
        group.forEach((n, i) => {
            const r = gap * Math.sqrt(i);
            n.position({ x: r * Math.cos(i * golden), y: r * Math.sin(i * golden) });
        });
        const eles = cy.collection(group);
        const own = eles.edgesWith(eles).not('.hidden');
        inside.merge(own);
        if (group.length > 2 && own.nonempty()) await settled(springs(eles.union(own), group.length, spacing, { numIter: 400 }));
        const heart = cy.getElementById(id);
        const cluster = group[0].data('cluster') ?? (heart.nonempty() ? heart.data('cluster') : null) ?? '';
        list.push({ id, eles, cluster });
    }

    // A cluster no group reaches — records linked only among themselves — is an island of
    // its own, rather than waiting for neighbours that will never be placed.
    const orphans = new Set();
    for (const part of visible.components()) {
        const parted = part.nodes().filter(n => !n.isParent());
        if (parted.length > 1 && parted.every(n => !home.has(n.id()))) {
            const group = parted.toArray().sort((a, b) => (b.data('degree') || 0) - (a.data('degree') || 0) || byId(a, b));
            group.forEach((n, i) => {
                const r = gap * Math.sqrt(i);
                n.position({ x: r * Math.cos(i * golden), y: r * Math.sin(i * golden) });
                orphans.add(n.id());
            });
            const eles = cy.collection(group);
            const own = eles.edgesWith(eles).not('.hidden');
            inside.merge(own);
            if (group.length > 2) await settled(springs(eles.union(own), group.length, spacing, { numIter: 400 }));
            list.push({ id: group[0].id(), eles, cluster: '' });
        }
    }

    // cose packs whatever is not connected on its own terms, so a record with no link
    // inside its island would be sent away from it. It is put back, round the shore.
    list.forEach(i => {
        const tied = i.eles.filter(n => n.connectedEdges().intersection(inside).nonempty());
        const loose = i.eles.not(tied);
        if (loose.nonempty()) {
            const core = tied.nonempty() ? tied.boundingBox({ includeLabels: false }) : { x1: 0, y1: 0, w: 0, h: 0 };
            const cx = core.x1 + core.w / 2;
            const cy0 = core.y1 + core.h / 2;
            const shore = Math.max(core.w, core.h) / 2;
            loose.forEach((n, j) => {
                const r = shore + gap * Math.sqrt(j + 1);
                n.position({ x: cx + r * Math.cos(j * golden), y: cy0 + r * Math.sin(j * golden) });
            });
        }
        i.box = i.eles.boundingBox({ includeLabels: false });
    });

    // Packed in rows: a cluster's islands together, the biggest cluster first, rows as
    // wide as the frame is for its height — tried, not guessed, since islands differ.
    const weight = new Map();
    list.forEach(i => weight.set(i.cluster, (weight.get(i.cluster) ?? 0) + i.eles.length));
    list.sort((a, b) => weight.get(b.cluster) - weight.get(a.cluster)
        || (a.cluster < b.cluster ? -1 : a.cluster > b.cluster ? 1 : 0)
        || b.eles.length - a.eles.length || (a.id < b.id ? -1 : 1));
    const pack = (rowWidth, move) => {
        let x = 0, y = 0, rowHeight = 0, width = 0;
        const put = (eles, box) => {
            if (x > 0 && x + box.w > rowWidth) {
                x = 0;
                y += rowHeight + gap * 2;
                rowHeight = 0;
            }
            if (move) {
                const dx = x - box.x1;
                const dy = y - box.y1;
                eles.forEach(n => n.position({ x: n.position('x') + dx, y: n.position('y') + dy }));
            }
            width = Math.max(width, x + box.w);
            x += box.w + gap * 2;
            rowHeight = Math.max(rowHeight, box.h);
        };
        list.forEach(i => put(i.eles, i.box));
        return { put, scale: Math.min(aspect / Math.max(width, 1), 1 / Math.max(y + rowHeight, 1)) };
    };
    const widest = Math.max(...list.map(i => i.box.w), 1);
    const total = list.reduce((sum, i) => sum + i.box.w + gap * 2, 0);
    let rowWidth = widest;
    for (let k = 0, best = 0; k <= 48; k++) {
        const candidate = widest + (total - widest) * k / 48;
        const { scale } = pack(candidate, false);
        if (scale > best * 1.001) {
            best = scale;
            rowWidth = candidate;
        }
    }
    const { put } = pack(rowWidth, true);

    // The bridges, between the islands they join: at their neighbours' middle, pushed
    // out of any island that middle falls inside, to its shore. A pass at a time, so a
    // cluster's heart finds its islands and then its people find it.
    const shores = list.map(i => {
        const b = i.eles.boundingBox({ includeLabels: false });
        return { x: b.x1 + b.w / 2, y: b.y1 + b.h / 2, r: Math.max(b.w, b.h) / 2 + gap / 2 };
    });
    const placed = new Set([...home.keys(), ...orphans]);
    // A bridge is kept a record's breadth from everything already placed — an island's
    // members and the bridges before it — or two records land on top of each other.
    // A grid of what is placed, so the check stays local in a large graph.
    const cellSize = 64;
    const occupied = new Map();
    const occupy = (x, y, r) => {
        const key = Math.floor(x / cellSize) * 65536 + Math.floor(y / cellSize);
        if (!occupied.has(key)) occupied.set(key, []);
        occupied.get(key).push({ x, y, r });
    };
    const clear = (px, py, r) => {
        const cx = Math.floor(px / cellSize), cy1 = Math.floor(py / cellSize);
        for (let gx = cx - 1; gx <= cx + 1; gx++) {
            for (let gy = cy1 - 1; gy <= cy1 + 1; gy++) {
                for (const b of occupied.get(gx * 65536 + gy) ?? []) {
                    if (Math.hypot(px - b.x, py - b.y) < r + b.r) return false;
                }
            }
        }
        return true;
    };
    nodes.filter(n => placed.has(n.id())).forEach(n => occupy(n.position('x'), n.position('y'), Math.max(n.data('size') || 20, 20) * 0.75));
    let waiting = nodes.filter(n => !placed.has(n.id())).toArray().sort((a, b) => (b.data('degree') || 0) - (a.data('degree') || 0) || byId(a, b));
    for (let pass = 0; pass < 3 && waiting.length > 0; pass++) {
        const still = [];
        waiting.forEach((n, i) => {
            const near = n.neighborhood().nodes().not('.hidden').filter(o => placed.has(o.id()));
            if (near.empty()) {
                still.push(n);
                return;
            }
            const mx = near.reduce((s, o) => s + o.position('x'), 0) / near.length;
            const my = near.reduce((s, o) => s + o.position('y'), 0) / near.length;
            const r = Math.max(n.data('size') || 20, 20) * 0.75 + 6;
            let px = mx + gap * Math.cos(i * golden), py = my + gap * Math.sin(i * golden);
            for (let k = 1; k < 32 && !clear(px, py, r); k++) {
                const a = (i + k) * golden, d = gap * (1 + k * 0.35);
                px = mx + d * Math.cos(a);
                py = my + d * Math.sin(a);
            }
            for (const shore of shores) {
                const d = Math.hypot(px - shore.x, py - shore.y);
                if (d < shore.r) {
                    const a = d > 0 ? Math.atan2(py - shore.y, px - shore.x) : i * golden;
                    px = shore.x + shore.r * Math.cos(a);
                    py = shore.y + shore.r * Math.sin(a);
                }
            }
            n.position({ x: px, y: py });
            occupy(px, py, r);
            placed.add(n.id());
        });
        waiting = still;
    }

    // What touches nothing on the map is one more island, last.
    if (waiting.length > 0) {
        waiting.forEach((n, i) => {
            const r = gap * Math.sqrt(i);
            n.position({ x: r * Math.cos(i * golden), y: r * Math.sin(i * golden) });
        });
        const eles = cy.collection(waiting);
        put(eles, eles.boundingBox({ includeLabels: false }));
    }
    return true;
}
