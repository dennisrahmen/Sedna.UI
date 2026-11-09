/* ── Relative time and countdowns ─────────────────────────────────────────────
   A `<time>` the app wrote, kept current: how long ago it was, or how long is left.

     <time datetime="2026-11-08T14:05:00Z" data-relative
           data-tip="8 Nov 2026, 15:05"
           data-seconds="just now"
           data-minutes-one="{n} minute ago" data-minutes="{n} minutes ago"
           data-hours="{n} h ago" data-days="{n} days ago">8 Nov 2026, 15:05</time>

     <time datetime="2026-11-08T14:35:00Z" data-countdown
           data-minutes="{n} min left" data-seconds="{n} s left">30 min left</time>

   THE WORDS ARE THE APP'S. Each unit the element shows has a format of the app's —
   `data-seconds`, `data-minutes`, `data-hours`, `data-days`, `data-weeks`,
   `data-months`, `data-years` — and the script picks the unit and puts the number
   where the format says `{n}`, formatted for the nearest `lang`. A format may also
   name a plural category of that language, `data-minutes-one`, `-two`, `-few`,
   `-many` or `-zero`, chosen through `Intl.PluralRules`; the bare attribute is every
   other number. A format without `{n}` is shown as it is. The script writes nothing
   else: a unit with no format is counted in the next smaller unit that has one, and
   an element with no formats at all is left as the app wrote it.

   The text the app wrote inside is what a reader sees before the script runs, and
   what a page without it keeps. The absolute time is the app's too — `data-tip` or
   `title` on the element. The element holds its text and nothing else: the script
   writes it into the element's own text node, so a Blazor render that leaves the
   text alone leaves the script's alone, and one that changes it is answered at once.

   A relative time counts whole units since, rounded down: "1 minute" from 60
   seconds to 119. A time still ahead reads as no time at all until it passes — a
   server clock a little ahead of the reader's. A countdown counts whole units left,
   also rounded down, but never to zero before it is zero: under the smallest unit it
   shows 1. When it reaches zero it shows 0 and dispatches a bubbling
   `sedna-countdown-end` with `detail.until`, the `datetime` it ended at — once per
   element and `datetime`, so a countdown already over when it is found reports it
   too, and one moved later starts again.

   One timer serves every element on the page. It sleeps until the soonest moment any
   of them changes — the next whole unit, or the next unit taking over — and not at
   all while the page is hidden; showing the page brings every element up to date,
   and a countdown that ended meanwhile dispatches then. Elements a render adds, and
   a `datetime`, a format or a `lang` a render changes, are picked up as they land.
   No member: the markup is the whole API. */
(function (ui) {

    var SECOND = 1000, MINUTE = 60 * SECOND, HOUR = 60 * MINUTE, DAY = 24 * HOUR;

    /* The units, smallest first. A unit takes over at its own length, so a month is
       thirty days and a year 365: what reads as "3 months" does not need a calendar. */
    var UNITS = [
        { name: 'seconds', size: SECOND },
        { name: 'minutes', size: MINUTE },
        { name: 'hours', size: HOUR },
        { name: 'days', size: DAY },
        { name: 'weeks', size: 7 * DAY },
        { name: 'months', size: 30 * DAY },
        { name: 'years', size: 365 * DAY }
    ];
    var CATEGORIES = ['zero', 'one', 'two', 'few', 'many'];
    var SELECTOR = 'time[data-relative], time[data-countdown]';

    /* The longest the timer sleeps. Every element would be correct without it; it keeps
       a timer inside what setTimeout can hold, and a page woken from sleep is put right
       by `visibilitychange` anyway. */
    var LONGEST = HOUR;

    var timer = 0;
    var shown = new WeakMap();
    var ended = new WeakMap();
    var formats = {};

    /* The formatters for a language, made once. A language the browser does not know
       falls back to its own, and a browser with no Intl gets plain digits. */
    function intl(locale) {
        var key = locale || '';
        if (formats[key]) return formats[key];
        var made = { number: null, plural: null };
        if (typeof Intl === 'object' && Intl) {
            try { made.number = new Intl.NumberFormat(locale || undefined); }
            catch (e) { try { made.number = new Intl.NumberFormat(); } catch (e2) { made.number = null; } }
            try { made.plural = new Intl.PluralRules(locale || undefined); }
            catch (e) { try { made.plural = new Intl.PluralRules(); } catch (e2) { made.plural = null; } }
        }
        formats[key] = made;
        return made;
    }

    function natural(ms) {
        var i = UNITS.length - 1;
        while (i > 0 && UNITS[i].size > ms) i--;
        return i;
    }

    function has(el, i) { return el.hasAttribute('data-' + UNITS[i].name); }

    /* The unit a span is counted in: its own when the app gave it a format, else the
       next smaller one that has one, else the smallest larger one. -1 for none. */
    function unitFor(el, ms) {
        var start = natural(ms), i;
        for (i = start; i >= 0; i--) if (has(el, i)) return i;
        for (i = start + 1; i < UNITS.length; i++) if (has(el, i)) return i;
        return -1;
    }

    function text(el, unit, n) {
        var host = el.closest('[lang]');
        var tools = intl(host ? host.getAttribute('lang') : '');
        var name = 'data-' + UNITS[unit].name;
        var format = null;
        if (tools.plural) {
            var category = tools.plural.select(n);
            if (CATEGORIES.indexOf(category) >= 0) format = el.getAttribute(name + '-' + category);
        }
        if (format === null) format = el.getAttribute(name);
        var number = tools.number ? tools.number.format(n) : String(n);
        return format.split('{n}').join(number);
    }

    /* What an element shows now, and how long until that changes. */
    function measure(el, now) {
        var at = Date.parse(el.getAttribute('datetime') || '');
        if (isNaN(at)) return null;
        var countdown = el.hasAttribute('data-countdown');
        var ms = countdown ? at - now : now - at;
        var ahead = 0;
        if (ms < 0) { ahead = -ms; ms = 0; }

        var unit = unitFor(el, ms);
        if (unit < 0) return null;
        var size = UNITS[unit].size;
        var n, wait;

        if (countdown) {
            n = ms > 0 ? Math.max(1, Math.floor(ms / size)) : 0;
            // The count drops as the span falls below a whole unit, and the unit changes
            // as it falls below the one in use: a millisecond past each. Under the last
            // whole unit the next change is the end itself, on the dot.
            wait = ms <= 0 ? Infinity : Math.floor(ms / size) === 0 ? ms : ms % size + 1;
            var floor = UNITS[natural(ms)].size;
            if (ms > floor) wait = Math.min(wait, ms - floor + 1);
        } else {
            n = Math.floor(ms / size);
            wait = size - ms % size;
            var next = natural(ms) + 1;
            if (next < UNITS.length) wait = Math.min(wait, UNITS[next].size - ms);
            wait += ahead;
        }

        return { text: text(el, unit, n), wait: wait, over: countdown && ms === 0 };
    }

    /* Into the element's own text node, so the node Blazor rendered stays the node it
       holds; any further text nodes are emptied rather than removed, for the same
       reason. */
    function write(el, value) {
        shown.set(el, value);
        var first = null;
        for (var node = el.firstChild; node; node = node.nextSibling) {
            if (node.nodeType !== 3) continue;
            if (!first) first = node;
            else if (node.nodeValue) node.nodeValue = '';
        }
        if (!first) el.appendChild(document.createTextNode(value));
        else if (first.nodeValue !== value) first.nodeValue = value;
    }

    function update() {
        clearTimeout(timer);
        timer = 0;
        if (document.visibilityState === 'hidden') return;

        var now = Date.now(), soonest = Infinity, done = [];
        var all = document.querySelectorAll(SELECTOR);
        for (var i = 0; i < all.length; i++) {
            var el = all[i], m = measure(el, now);
            if (!m) continue;
            write(el, m.text);
            if (m.over) {
                var until = el.getAttribute('datetime');
                if (ended.get(el) !== until) { ended.set(el, until); done.push(el); }
            } else {
                ended.delete(el);
            }
            if (m.wait < soonest) soonest = m.wait;
        }
        if (soonest !== Infinity) timer = setTimeout(update, Math.min(soonest, LONGEST));

        for (var j = 0; j < done.length; j++) {
            done[j].dispatchEvent(new CustomEvent('sedna-countdown-end', {
                bubbles: true,
                detail: { until: done[j].getAttribute('datetime') }
            }));
        }
    }

    /* Which mutations concern a timed element. The script's own writes come back as
       mutations too, and are recognised by the text it wrote. */
    function concerns(records) {
        for (var i = 0; i < records.length; i++) {
            var r = records[i], t = r.target;
            if (r.type === 'characterData') {
                var el = t.parentNode;
                if (el && el.nodeType === 1 && el.matches(SELECTOR) && t.nodeValue !== shown.get(el)
                    && t.nodeValue !== '') return true;
            } else if (r.type === 'attributes') {
                if (r.attributeName === 'lang' || (t.matches && t.matches(SELECTOR))) return true;
            } else {
                for (var j = 0; j < r.addedNodes.length; j++) {
                    var node = r.addedNodes[j];
                    if (node.nodeType !== 1) continue;
                    if (node.matches(SELECTOR) || node.querySelector(SELECTOR)) return true;
                }
                if (t.nodeType === 1 && t.matches(SELECTOR)) return true;
            }
        }
        return false;
    }

    function watch() {
        update();
        if (!('MutationObserver' in window)) return;
        var names = ['datetime', 'data-relative', 'data-countdown', 'lang'];
        for (var i = 0; i < UNITS.length; i++) {
            names.push('data-' + UNITS[i].name);
            for (var c = 0; c < CATEGORIES.length; c++) names.push('data-' + UNITS[i].name + '-' + CATEGORIES[c]);
        }
        new MutationObserver(function (records) {
            if (concerns(records)) update();
        }).observe(document.documentElement, {
            childList: true, subtree: true, characterData: true,
            attributes: true, attributeFilter: names
        });
    }

    document.addEventListener('visibilitychange', update);
    window.addEventListener('pageshow', update);

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', watch);
    else watch();

})(window.sednaUi);
