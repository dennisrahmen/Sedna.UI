namespace Sedna.UI;

/// <summary>
/// The semantic family a toast belongs to. Chooses its icon, its colour, and
/// whether it interrupts a screen reader.
/// </summary>
/// <remarks>
/// The families are the library's own: <c>go</c> sends something outward,
/// <c>warn</c> changes a control, <c>danger</c> is a failure. Only
/// <see cref="Danger"/> is announced assertively — a failure is worth cutting in
/// for and a success is not.
/// </remarks>
public enum ToastKind
{
    /// <summary>Neutral information. The default.</summary>
    Info,

    /// <summary>Something completed successfully.</summary>
    Go,

    /// <summary>Something completed, with a caveat worth reading.</summary>
    Warn,

    /// <summary>Something failed. Announced assertively.</summary>
    Danger,
}
