/* ── Icons on the canvas ──────────────────────────────────────────────────────
   `data-icon="ri-server-line"` puts the Remix Icon glyph inside a record. The canvas
   cannot use the icon font's classes, so the glyph is read from the class's own
   `::before` content, drawn once onto a small canvas in the colour it needs, and handed
   to cytoscape as an image. The font is the one the stylesheet already loads; a glyph
   asked for before it has arrived is drawn again once it has.

   Nothing is fetched: an icon the font does not have draws nothing, and says so once.
   ─────────────────────────────────────────────────────────────────────────── */

function iconPainter(host) {
    const probe = document.createElement('i');
    probe.setAttribute('aria-hidden', 'true');
    probe.style.cssText = 'position:absolute;width:0;height:0;overflow:hidden;visibility:hidden;pointer-events:none';
    host.appendChild(probe);
    const glyphs = new Map();
    const images = new Map();
    let fontReady = null;

    function glyph(cls) {
        if (!glyphs.has(cls)) {
            probe.className = cls;
            const style = getComputedStyle(probe, '::before');
            const content = style.content;
            const ch = content && content !== 'none' && content !== 'normal' ? content.replace(/^["']|["']$/g, '') : '';
            glyphs.set(cls, ch ? { ch, family: style.fontFamily } : null);
            if (!ch) warnOnce('icon:' + cls, `"${cls}" is not a Remix Icon class; the record is drawn without it.`);
        }
        return glyphs.get(cls);
    }

    return {
        /* Resolves once the icon font can draw — so the first paint has its icons.
           Settles either way: a page that blocks the font still gets its graph. */
        ready() {
            if (!fontReady) {
                fontReady = (document.fonts && document.fonts.load
                    ? document.fonts.load('32px remixicon').catch(() => null)
                    : Promise.resolve()).then(() => { glyphs.clear(); images.clear(); });
            }
            return fontReady;
        },
        // A data: URL of the glyph in `colour`, square, `px` CSS pixels at device resolution.
        image(cls, colour, px = 32) {
            if (!cls) return null;
            const key = cls + '|' + colour + '|' + px;
            if (images.has(key)) return images.get(key);
            const g = glyph(cls);
            if (!g) {
                images.set(key, null);
                return null;
            }
            const scale = Math.min(Math.max(window.devicePixelRatio || 1, 1), 3) * 2;
            const size = Math.round(px * scale);
            const canvas = document.createElement('canvas');
            canvas.width = canvas.height = size;
            const ctx = canvas.getContext('2d');
            ctx.fillStyle = colour;
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.font = `${Math.round(size * 0.86)}px ${g.family}`;
            ctx.fillText(g.ch, size / 2, size / 2 + size * 0.02);
            const url = canvas.toDataURL('image/png');
            images.set(key, url);
            return url;
        },
        clear: () => images.clear(),
        remove: () => probe.remove(),
    };
}
