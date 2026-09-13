namespace Sedna.UI;

/// <summary>
/// Renders one <c>&lt;style&gt;</c> containing <see cref="SednaUiBrand.ToCss(SednaUiOptions)"/>
/// for the themes registered with <c>AddSednaUi</c>.
/// </summary>
/// <remarks>
/// <para>
/// Place it in <c>&lt;head&gt;</c>:
/// <code>
/// &lt;head&gt;
///     &lt;SednaBrandStyle /&gt;
/// &lt;/head&gt;
/// </code>
/// It renders as part of the server-rendered document, not from JavaScript after first paint —
/// the same reason <c>CLAUDE.md</c> forbids a runtime loader for the CSS parts applies here: a
/// brand injected after first paint shows the wrong colours on every load.
/// </para>
/// <para>
/// An infrastructure component, in the sense of <b>Markup belongs to the app</b> in the root
/// <c>CLAUDE.md</c>: it emits a <c>&lt;style&gt;</c> element, not markup a page reader needs to
/// see or copy, so the app still authors everything it renders. It has no <c>.razor.css</c>, and never should — see <c>build/verify-package.sh</c>'s
/// scoped-CSS guard.
/// </para>
/// </remarks>
public partial class SednaBrandStyle;
