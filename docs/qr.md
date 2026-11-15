# QR code

The app's text or address as a QR code, for a camera to read. A tier 3 surface — see
[surfaces.md](surfaces.md) — delivered **drawn**: Sedna.UI's own encoder draws the symbol as an SVG,
with no engine behind it. The app writes the element, its name and the human-readable text beside it.

## Markup

```html
<figure class="qr-figure">
  <span class="qr" data-qr="https://example.com/pair/7Q4K" aria-label="QR code to pair the device"></span>
  <figcaption class="qr-caption">example.com/pair/7Q4K</figcaption>
</figure>
```

| Attribute | Values |
|---|---|
| `data-qr` | The text to encode. Change it and the code is drawn again |
| `data-qr-level` | The error correction level: `L` (7% of the symbol can be lost), `M` (15%, the default), `Q` (25%), `H` (30%). A logo raises it to at least `Q` |
| `data-qr-shape` | The shape of a module: `square` (the default), `rounded` or `dots` |
| `data-qr-eye` | The shape of the three finder patterns: `square` (the default), `rounded` or `round` |
| `data-qr-invert` | Allows an inverted code: ink lighter than the paper. See **Contrast** |
| `data-qr-eager` | Start at once rather than when the code comes within a screen of the viewport |
| `aria-label` | Required. The code draws no words, so its name is the app's: say what scanning it does |

The script writes these on the element:

| Attribute | Means |
|---|---|
| `data-qr-state` | `ready`; `empty` when `data-qr` is empty; `overflow` when the text is more than the largest symbol holds at that level, and nothing is drawn |
| `data-qr-version` | The symbol's version, 1 to 40 — how dense it is. Absent unless `ready` |
| `data-qr-modules` | The modules across the symbol, the quiet zone not counted |
| `data-qr-mode` | How the text is held: `numeric`, `alphanumeric`, `byte` or `mixed` |
| `data-qr-contrast` | How the colours were judged: `ok`, `inverted` or `fallback`. See **Contrast** |

`role="img"` is added when the element has no role.

| Class | Size |
|---|---|
| `.qr--sm` | 6rem |
| `.qr` | 10rem |
| `.qr--lg` | 15rem |

`--qr-size` sets any other size. The size is the whole square, the quiet zone included. A denser code
needs a bigger square — keep a module at least two device pixels, and prefer a short address to a large
square.

`.qr-figure` stacks a code over its `.qr-caption`, the text the code holds, written out by the app for
the reader who cannot scan and the one who wants to check where it goes.

## What a code holds

A code holds text; what the text is decides what a phone does with it. The library encodes any text.

### Modes

ISO/IEC 18004, as written. The text is cut into segments, each in the mode that takes the fewest bits:

- **numeric** for digits — 10 bits for three;
- **alphanumeric** for digits, capitals and `` $%*+-./:`` — 11 bits for two;
- **byte** for anything else, as UTF-8, with no ECI header; readers take a byte-mode payload that is valid
  UTF-8 as UTF-8. Kanji mode is not used: Japanese is byte mode, which every reader decodes.

Where the text is mixed the segments are chosen by dynamic programming over the characters, with the cost
of a segment's header counted, so a long number at the end of an address gets a numeric segment of its own
and a short run of digits does not get a header it does not pay for. Nothing is configured: `data-qr-mode`
says what was chosen.

The level is the app's and is raised only for a logo. The **smallest version** — 1 to 40 — that holds the
text at that level is used. The **mask** is the one of the eight with the lowest penalty under the
standard's four rules, scored with its format information drawn; of equals, the first.

The most a code holds is 2953 bytes at `L`, 2331 at `M`, 1663 at `Q` and 1273 at `H` — more when the text
is digits or capitals. A text that does not fit draws nothing and sets `data-qr-state="overflow"`, and the
console says so.

### Standard payloads

`SednaQrPayload` writes the standard strings and escapes what would end a field early. Each member is a
pure function that returns the text for `data-qr`; nothing needs it, since the formats are plain text:

```razor
<span class="qr" data-qr="@SednaQrPayload.WiFi("Guest network", "correct horse battery")"
      aria-label="QR code that joins the guest Wi-Fi"></span>
```

| Member | Writes |
|---|---|
| `Url(url)` | The address, which must be absolute |
| `WiFi(ssid, password, security, hidden)` | `WIFI:T:WPA;S:Guest network;P:secret;;` — `security` is a `SednaWiFiSecurity`: `None`, `Wpa` (WPA and WPA2), `Wpa3` (`SAE`) or `Wep`. `\ ; , : "` are escaped |
| `Email(address, subject, body, cc)` | `mailto:support@example.com?subject=…&body=…`, percent-encoded as RFC 6068 says |
| `Phone(number)` | `tel:+4930123456` — spaces, dashes, dots and brackets dropped |
| `Sms(number, message)` | `sms:+4930123456?body=…` (RFC 5724) |
| `MeCard(givenName, familyName, phone, email, url, address, note)` | `MECARD:N:Fischer,Alex;TEL:…;;` — the short contact format, a smaller code than a vCard |
| `VCard(givenName, familyName, organisation, title, phone, email, url, address, note)` | A vCard 3.0, CR LF lines, escaped text |
| `Geo(latitude, longitude, uncertaintyMetres)` | `geo:52.52,13.405` (RFC 5870), always in the invariant culture |
| `Event(summary, start, end, location, description, allDay)` | An iCalendar `VEVENT` in a `VCALENDAR`, in UTC, or in whole days |

A value the app does not have is `null` and is left out; one that makes the string meaningless — an
empty SSID, an open network with a password — throws. An HTML attribute turns a line break in its value
into a bare line feed, which phones accept for a contact or an event.

## Look

**Module shape.** `data-qr-shape="rounded"` rounds a corner where neither neighbour beside it is dark, so
a column of modules is a bar with round ends and a lone module is a disc; `dots` draws every module as a
disc a little smaller than the module. **Eye shape.** `data-qr-eye` draws the three finder patterns —
the ring and the stone in it — `square`, `rounded` or `round`. They combine freely; every combination is
read back by a decoder in the tests. Squares are the most forgiving of a poor camera.

**Quiet zone.** `--qr-quiet` is the paper round the symbol in modules, four by default, which is what a
reader expects. A smaller token works with many cameras and fails with some, and is the app's risk. The
square keeps its size, so a wider quiet zone makes the symbol smaller inside it.

**Logo.** A child of the code with the class `qr-logo` — an `<img>`, an `<svg>`, or a box holding one —
is centred over a square of modules that are not drawn:

```html
<span class="qr qr--lg" data-qr="https://example.com/pair/7Q4K" aria-label="QR code to pair the device">
  <img class="qr-logo" src="/logo.png" alt="">
</span>
```

The error correction fills the square in again for a reader, so a logo costs strength, and the library
limits it:

- the level is at least `Q` — `L` and `M` are raised to `Q`, and `H` is kept;
- the symbol is at least version 5, so a short text still leaves room round the square;
- the square takes at most 4.5% of the symbol's modules at `Q` and 7.5% at `H`, however large
  `--qr-logo-size` — the share of the symbol's side the app would like, `0.22` by default — asks;
- the logo is two modules smaller than the square, leaving a light module of paper all round it.

The tests wipe a square a third larger than the one the code leaves out and read the code anyway.
Give the logo a transparent or paper-coloured background, and no text a reader has to make out.

## Colour

A camera reads dark modules on light paper, so the paper stays light and the ink dark in every variant.
`--qr-ink` and `--qr-paper` are the two colours; set them on the element or in the app's `brand.css`.

| Class | Ink |
|---|---|
| `.qr--brand` | A deep shade of the app's `--brand` |
| `.qr--go`, `.qr--warn`, `.qr--danger`, `.qr--info`, `.qr--secret` | The deep step of the tone's ramp |
| `.qr--cyan`, `.qr--orange`, `.qr--teal` | The deep step of the hue's ramp |
| `.qr--navy`, `.qr--orbit`, `.qr--coral` | Sedna Navy, and the deep steps of Orbit Blue and Sedna Red |

A tone class uses a palette step no variant remaps, not the tone's own token — those lighten in a dark
variant, and a code does not. Under forced colours a code keeps its colours, as an image does. It prints
as it shows, and never across two pages.

### Contrast

The script measures the colours the browser resolves for `--qr-ink` and `--qr-paper`, whatever notation
they were written in, when a code is drawn and again whenever its class or style, the document's
variant, colour-vision or contrast setting, or the browser's own light and dark change.

- A ratio under **4:1**, or an ink or paper that is not opaque, is drawn as the **safe pair**:
  `--qr-ink-safe` on `--qr-paper-safe` — dark ink on white — and `data-qr-contrast="fallback"`.
- Ink **lighter than the paper** is an inverted code, which many readers cannot read at all. It is drawn
  only when the element has `data-qr-invert`, as `data-qr-contrast="inverted"`; otherwise it is the safe
  pair. **Do not invert a code that has to work for everybody** — a phone's own camera app mostly copes;
  a handheld scanner or an older app does not.

The console says why a code fell back. `ok` means the colours were left as the app set them.

## Saving

An `svg` or a `png` of the code, with the colours it shows, at the size asked for. The picture has the
quiet zone, the shapes and the logo, and its colours are plain values — read from the browser, not
variables — so it opens in a viewer that has no stylesheet.

A button or a link the app wrote:

```html
<figure class="qr-figure">
  <span class="qr" id="pair" data-qr="https://example.com/pair/7Q4K" aria-label="QR code to pair the device"></span>
  <button class="btn" type="button" data-qr-download="svg">SVG</button>
  <button class="btn" type="button" data-qr-download="png" data-qr-size="1000" data-qr-name="pair-dock-3">PNG</button>
</figure>
```

| Attribute | Means |
|---|---|
| `data-qr-download` | `svg` or `png`. Delegated from the document, so a button rendered later needs no wiring |
| `data-qr-for` | The id of the code, on the button or on a container around it. Without it: the code in the nearest `.qr-figure` or `[data-qr-frame]`, else the one the button sits inside |
| `data-qr-size` | The picture's side in pixels, 16 to 8192; 1024 by default. A whole multiple of `data-qr-modules` plus twice `--qr-quiet` makes every module whole pixels, which reads best |
| `data-qr-name` | The file's name without its extension; `qr-code` by default |

From script, for an app's own control:

| Member | |
|---|---|
| `sednaUi.qr.download(target, { format, size, name })` | Saves it. A promise of `{ format, size, name }`; rejects with the reason |
| `sednaUi.qr.get(target)` | A promise of the code's handle, starting it if need be. `version`, `level`, `mask`, `modules`, `mode`, `redraw()`, `toSvg(options)` (the SVG document as text), `toBlob(options)` and `download(options)` |
| `sednaUi.qr.init(root)` | Starts every code in `root` at once. A promise of how many |

The code dispatches bubbling events: `sedna-qr-download` with `{ format, size, name }` once the file is
handed to the browser, and `sedna-qr-error` with `{ message }` when it could not be made — an
unsupported format, a size out of range, or a logo that cannot be read.

**The logo** goes into the file as an image. An `<img>` is read through a canvas, which a page may not
do to a picture from another origin: serve it from the same origin or write it as a data URL. An inline
`<svg>` is written out as it is and must carry its own fills — a file has no stylesheet to give it any. A
logo that cannot be read stops the save rather than leaving a hole in the code.

Saving needs no .NET: the script owns it and nothing is held across the boundary.

## Lifecycle

`js-parts/48-qr.js` registers `[data-qr]` with the surface loader, which imports `Sedna.UI.qr.js`
relative to the main script the first time a page shows one, starts each code as it comes within a
screen of the viewport, and disposes one whose element has gone. On a prerendered Blazor page codes
wait for the interactive render, as every surface does. The front door also owns the save buttons and
`sednaUi.qr`, so a page that never shows a code never downloads the encoder.

## Without the script

An empty light square. The caption the app writes beside the code is the alternative, there and for
every reader who cannot scan.
