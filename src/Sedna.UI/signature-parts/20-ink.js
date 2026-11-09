/* ── The ink ──────────────────────────────────────────────────────────────────
   `--signature-ink` and `--signature-ink-width`, read through a probe inside the pad,
   so the pad's own tokens, the variant, colour vision, contrast, forced colours and the
   app's brand all reach the canvas. The browser resolves whatever the token holds; the
   width is read as the probe's computed width, so any length works. Read when a pad
   starts and whenever anything that colours it changes — never per stroke.
   ─────────────────────────────────────────────────────────────────────────── */
function readInk(p) {
    const probe = p.probe, style = getComputedStyle(probe);
    probe.style.color = 'var(--signature-ink)';
    probe.style.width = 'var(--signature-ink-width)';
    p.ink = style.color;
    const w = parseFloat(style.width);
    p.inkWidth = w > 0 ? w : 2;
}
