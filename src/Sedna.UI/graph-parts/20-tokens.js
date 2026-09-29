/* ── Colour, from the tokens ──────────────────────────────────────────────────
   A canvas cannot paint `var(--viz-2)`. A probe element can resolve it: the browser
   works the token out through every layer of the theme — variant, colour vision,
   contrast, forced colours, an app's own brand file — and a one-pixel canvas turns
   whatever it serialises as (`rgb()`, `color(srgb …)` for a color-mix() token) into the
   rgba() cytoscape parses. Cached per token until the theme changes.

   The background a label is outlined against is not a token at all: it is whatever
   the graph actually sits on, read from the nearest ancestor that paints one. A graph
   in a card outlines its labels in the card's colour, one on the page in the page's.
   ─────────────────────────────────────────────────────────────────────────── */

function colourReader(host) {
    const probe = document.createElement('span');
    probe.setAttribute('aria-hidden', 'true');
    probe.style.cssText = 'position:absolute;width:0;height:0;overflow:hidden;visibility:hidden;pointer-events:none';
    host.appendChild(probe);
    const pixel = document.createElement('canvas');
    pixel.width = pixel.height = 1;
    const ctx = pixel.getContext('2d', { willReadFrequently: true });
    const cache = new Map();

    // Whatever the browser serialised, as rgba() — by painting it and reading it back.
    function rgba(value, fallback) {
        if (!value || value === 'transparent') return fallback;
        if (/^rgba?\(/.test(value) && !/\//.test(value)) return value;
        // A value the canvas cannot parse would leave the previous fill in place; it is
        // painted as transparent instead, which reads back as the fallback.
        ctx.clearRect(0, 0, 1, 1);
        ctx.fillStyle = CSS.supports('color', value) ? value : 'transparent';
        ctx.fillRect(0, 0, 1, 1);
        const [r, g, b, a] = ctx.getImageData(0, 0, 1, 1).data;
        if (a === 0) return fallback;
        return a === 255 ? `rgb(${r}, ${g}, ${b})` : `rgba(${r}, ${g}, ${b}, ${(a / 255).toFixed(3)})`;
    }

    return {
        token(name, fallback) {
            if (!name) return fallback;
            if (!cache.has(name)) {
                probe.style.color = '';
                probe.style.color = `var(${name})`;
                cache.set(name, rgba(getComputedStyle(probe).color, null));
            }
            return cache.get(name) ?? fallback;
        },
        // The colour behind the graph: the first ancestor that paints a background.
        ground(fallback) {
            if (!cache.has('@ground')) {
                let value = null;
                for (let el = host; el && el.nodeType === 1; el = el.parentElement) {
                    const bg = getComputedStyle(el).backgroundColor;
                    if (bg && bg !== 'transparent' && !/rgba\(\s*0,\s*0,\s*0,\s*0\s*\)/.test(bg)) {
                        value = rgba(bg, null);
                        if (value) break;
                    }
                }
                cache.set('@ground', value ?? this.token('--surface-content', null) ?? this.token('--bg', null));
            }
            return cache.get('@ground') ?? fallback;
        },
        font() {
            if (!cache.has('@font')) cache.set('@font', getComputedStyle(host).fontFamily || 'system-ui, sans-serif');
            return cache.get('@font');
        },
        clear: () => cache.clear(),
        remove: () => probe.remove(),
        probe,
    };
}

/* The neutral colours every drawing uses, resolved once per paint. Tokens only, and no
   literal behind them: a token the page does not declare leaves the probe's colour to
   inherit, so it reads as the text around the graph rather than failing. */
function palette(colours) {
    const c = (token, fallback) => colours.token(token, fallback);
    const fg = c('--fg');
    return {
        fg,
        soft: c('--fg-soft', fg),
        muted: c('--muted', fg),
        line: c('--border-strong', fg),
        border: c('--border', fg),
        brand: c('--brand', fg),
        accent: c('--accent', fg),
        ring: c('--brand-ring', fg),
        raised: c('--surface-raised-2', c('--card-bg')),
        tint: c('--brand-tint', c('--card-bg')),
        ground: colours.ground(c('--card-bg')),
        font: colours.font(),
    };
}

// A colour with its alpha replaced — for a group's fill, drawn at a whisper of its tone.
function withAlpha(colour, alpha) {
    const m = /rgba?\(\s*(\d+),\s*(\d+),\s*(\d+)/.exec(colour || '');
    return m ? `rgba(${m[1]}, ${m[2]}, ${m[3]}, ${alpha})` : colour;
}
