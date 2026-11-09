/* ── Numbers and scales ───────────────────────────────────────────────────────
   The one thing the chart writes that the app did not is the numbers on an axis —
   a tick is no value in the data. They are written in the page's language, through
   `Intl.NumberFormat`, with the options the app names in `data-chart-format` as JSON:

     data-chart-format='{"notation":"compact"}'
     data-chart-format='{"style":"currency","currency":"EUR","maximumFractionDigits":0}'
     data-chart-format='{"style":"percent"}'

   A value in the data is said in the app's own words — the cell's text — and the
   format only writes one the app gave as a bare number, in JSON or a call.

   The value axis runs over nice steps (1, 2, 2.5, 5 × 10ⁿ), as many as there is room
   for, and takes in zero unless `data-chart-min` says otherwise: a bar that does not
   start at zero misstates its value. `data-chart-min` and `data-chart-max` fix either
   end; `data-chart-min="auto"` lets a line or a scatter start near its lowest value.
   ─────────────────────────────────────────────────────────────────────────── */

function languageOf(el) {
    const holder = el.closest('[lang]');
    return (holder && holder.getAttribute('lang')) || document.documentElement.lang || undefined;
}

function formatter(el) {
    const lang = languageOf(el);
    let options = {};
    const raw = el.getAttribute('data-chart-format');
    if (raw) {
        try {
            options = JSON.parse(raw) || {};
        } catch (e) {
            warnOnce('format:' + raw, 'data-chart-format is not JSON. Write the Intl.NumberFormat options, e.g. {"notation":"compact"}.');
        }
    }
    let full, percent;
    try {
        full = new Intl.NumberFormat(lang, options);
    } catch (e) {
        warnOnce('format-options:' + raw, 'data-chart-format names options Intl.NumberFormat refuses.');
        full = new Intl.NumberFormat(lang);
    }
    try { percent = new Intl.NumberFormat(lang, { style: 'percent', maximumFractionDigits: 1 }); }
    catch (e) { percent = new Intl.NumberFormat(undefined, { style: 'percent' }); }
    // A tick needs no more digits than its step has, and no trailing zeros.
    const ticks = new Map();
    return {
        lang,
        value: v => full.format(v),
        percent: v => percent.format(v),
        /* Every tick of an axis, with as many digits as it takes for no two to read
           alike — a compact `2K` beside `2K` says nothing. */
        ticksOf(axis) {
            let out = [];
            for (let extra = 0; extra <= 3; extra++) {
                out = axis.ticks.map(t => this.tick(t, axis.step, extra));
                if (new Set(out).size === out.length) break;
            }
            return out;
        },
        tick(v, step, extra = 0) {
            const decimals = (String(clean(Math.abs(step))).split('.')[1] || '').length;
            // A percentage is a hundred times the number, so it needs two digits fewer.
            const digits = Math.max(0, Math.min(6, decimals + extra - (options.style === 'percent' ? 2 : 0)));
            const key = digits;
            if (!ticks.has(key)) {
                const o = Object.assign({}, options, { maximumFractionDigits: digits });
                o.minimumFractionDigits = Math.min(o.minimumFractionDigits ?? 0, digits);
                delete o.maximumSignificantDigits;
                delete o.minimumSignificantDigits;
                let f;
                try { f = new Intl.NumberFormat(lang, o); } catch (e) { f = full; }
                ticks.set(key, f);
            }
            return ticks.get(key).format(v);
        },
    };
}

const clean = v => +v.toFixed(10);

function niceStep(span, count) {
    const raw = span / Math.max(1, count);
    const mag = Math.pow(10, Math.floor(Math.log10(raw)));
    const f = raw / mag;
    return (f <= 1 ? 1 : f <= 2 ? 2 : f <= 2.5 ? 2.5 : f <= 5 ? 5 : 10) * mag;
}

/* Ticks from lo to hi, about `count` of them, widened to whole steps. */
function niceAxis(lo, hi, count, fixed) {
    if (!Number.isFinite(lo) || !Number.isFinite(hi)) { lo = 0; hi = 1; }
    if (lo === hi) {
        if (lo === 0) hi = 1;
        else if (lo > 0) lo = fixed.min ?? 0;
        else hi = fixed.max ?? 0;
        if (lo === hi) { lo -= 1; hi += 1; }
    }
    const step = niceStep(hi - lo, count);
    const a = fixed.min ?? clean(Math.floor(lo / step + 1e-9) * step);
    const b = fixed.max ?? clean(Math.ceil(hi / step - 1e-9) * step);
    const ticks = [];
    const first = clean(Math.ceil(a / step - 1e-9) * step);
    for (let t = first; t <= b + step * 1e-6; t = clean(t + step)) ticks.push(t);
    return { lo: a, hi: b > a ? b : a + step, step, ticks };
}

function attrNumber(el, name) {
    const raw = el.getAttribute(name);
    return raw === null || raw === 'auto' ? null : plainNumber(raw);
}

/* A linear scale from data to pixels. */
function linear(lo, hi, from, to) {
    const span = hi - lo || 1;
    const f = v => from + ((v - lo) / span) * (to - from);
    f.lo = lo; f.hi = hi; f.from = from; f.to = to;
    return f;
}
