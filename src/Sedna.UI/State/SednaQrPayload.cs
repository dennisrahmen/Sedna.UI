using System.Globalization;
using System.Text;

namespace Sedna.UI;

/// <summary>The security a Wi-Fi network asks for, as a phone reads it from a QR code.</summary>
public enum SednaWiFiSecurity
{
    /// <summary>An open network, with no password.</summary>
    None,

    /// <summary>WPA, WPA2 and mixed-mode WPA/WPA2 networks, which every phone reads as <c>WPA</c>.</summary>
    Wpa,

    /// <summary>WPA3 (SAE) networks.</summary>
    Wpa3,

    /// <summary>A WEP network.</summary>
    Wep,
}

/// <summary>
/// The text of the standard QR payloads — a Wi-Fi network, an e-mail, a phone number, a text
/// message, a contact, a place, an event — written and escaped the way a phone camera reads them.
/// </summary>
/// <remarks>
/// <para>
/// A state helper: pure functions that return the string to put in <c>data-qr</c>, with no
/// interop and nothing held. The element stays the app's markup:
/// </para>
/// <code>
/// &lt;span class="qr" data-qr="@SednaQrPayload.WiFi("Guest network", "correct horse")"
///       aria-label="QR code to join the Wi-Fi"&gt;&lt;/span&gt;
/// </code>
/// <para>
/// None of this is needed: a payload is plain text, and the format of each is written out beside
/// its method, so an app may write the string itself. What the helpers add is the escaping — a
/// <c>;</c> in a network's password ends the field it is in — and the formats a phone is
/// known to read. Text is UTF-8 in the code, so any language is carried as it is.
/// </para>
/// <para>
/// An address, a number or a name that is empty is a mistake and throws; a value an app does
/// not have is <see langword="null"/> and is left out.
/// </para>
/// </remarks>
public static class SednaQrPayload
{
    /// <summary>
    /// A web address: <c>https://example.com/pair/7Q4K</c>. The text itself; a phone offers to
    /// open it.
    /// </summary>
    /// <exception cref="ArgumentException">The address is not an absolute URL.</exception>
    public static string Url(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        var trimmed = url.Trim();

        // A path that starts with a slash is an absolute file path to .NET on some systems; here it has no scheme.
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || uri.Scheme.Length < 2
            || !trimmed.StartsWith(uri.Scheme + ":", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A QR code opens an absolute address, with its scheme.", nameof(url));
        }

        return trimmed;
    }

    /// <summary>
    /// A Wi-Fi network: <c>WIFI:T:WPA;S:Guest network;P:secret;H:true;;</c>. A phone offers to
    /// join it. The name and the password are escaped; <paramref name="hidden"/> is for a network
    /// that does not announce its name.
    /// </summary>
    /// <param name="ssid">The network's name.</param>
    /// <param name="password">Its password; leave out for an open network.</param>
    /// <param name="security">What the network asks for. <see cref="SednaWiFiSecurity.Wpa"/> unless <paramref name="password"/> is <see langword="null"/>.</param>
    /// <param name="hidden">Whether the network hides its name.</param>
    /// <exception cref="ArgumentException">An open network was given a password, or a protected one none.</exception>
    public static string WiFi(string ssid, string? password = null, SednaWiFiSecurity? security = null, bool hidden = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(ssid);
        var kind = security ?? (password is null ? SednaWiFiSecurity.None : SednaWiFiSecurity.Wpa);
        if (kind == SednaWiFiSecurity.None && !string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("An open network has no password.", nameof(password));
        }

        if (kind != SednaWiFiSecurity.None && string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("A protected network needs its password.", nameof(password));
        }

        var type = kind switch
        {
            SednaWiFiSecurity.None => "nopass",
            SednaWiFiSecurity.Wep => "WEP",
            SednaWiFiSecurity.Wpa3 => "SAE",
            _ => "WPA",
        };
        var sb = new StringBuilder("WIFI:T:").Append(type).Append(";S:").Append(EscapeWiFi(ssid)).Append(';');
        if (kind != SednaWiFiSecurity.None) sb.Append("P:").Append(EscapeWiFi(password!)).Append(';');
        if (hidden) sb.Append("H:true;");
        return sb.Append(';').ToString();
    }

    /// <summary>
    /// An e-mail to write: <c>mailto:support@example.com?subject=Pairing&amp;body=…</c>. Percent-encoded
    /// as RFC 6068 says.
    /// </summary>
    public static string Email(string address, string? subject = null, string? body = null, string? cc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        var sb = new StringBuilder("mailto:").Append(MailAddress(address));
        var sep = '?';
        void Add(string name, string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            sb.Append(sep).Append(name).Append('=').Append(Percent(value));
            sep = '&';
        }

        Add("cc", cc is null ? null : MailAddress(cc));
        Add("subject", subject);
        Add("body", body);
        return sb.ToString();
    }

    /// <summary>
    /// A number to call: <c>tel:+4930123456</c>. Spaces, dashes, dots and brackets are dropped;
    /// write the country code with its <c>+</c>.
    /// </summary>
    /// <exception cref="ArgumentException">The number holds anything but digits and the usual separators.</exception>
    public static string Phone(string number) => "tel:" + Digits(number, nameof(number));

    /// <summary>
    /// A text message to send: <c>sms:+4930123456?body=…</c> (RFC 5724), which iOS and Android
    /// both open in their messages app.
    /// </summary>
    public static string Sms(string number, string? message = null)
    {
        var sb = new StringBuilder("sms:").Append(Digits(number, nameof(number)));
        if (!string.IsNullOrEmpty(message)) sb.Append("?body=").Append(Percent(message));
        return sb.ToString();
    }

    /// <summary>
    /// A contact in the short MeCard format — <c>MECARD:N:Fischer,Alex;TEL:…;EMAIL:…;;</c> —
    /// which a phone reads from a smaller, easier code than a vCard of the same person.
    /// </summary>
    /// <param name="givenName">The first name.</param>
    /// <param name="familyName">The family name.</param>
    /// <param name="phone">A phone number.</param>
    /// <param name="email">An e-mail address.</param>
    /// <param name="url">A web address.</param>
    /// <param name="address">A postal address, on one line.</param>
    /// <param name="note">A note.</param>
    public static string MeCard(string givenName, string? familyName = null, string? phone = null, string? email = null,
        string? url = null, string? address = null, string? note = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(givenName);
        var sb = new StringBuilder("MECARD:N:");
        sb.Append(EscapeMeCard(familyName ?? string.Empty));
        if (!string.IsNullOrEmpty(familyName)) sb.Append(',');
        sb.Append(EscapeMeCard(givenName)).Append(';');
        void Add(string field, string? value)
        {
            if (!string.IsNullOrEmpty(value)) sb.Append(field).Append(':').Append(EscapeMeCard(value)).Append(';');
        }

        Add("TEL", phone);
        Add("EMAIL", email);
        Add("URL", url);
        Add("ADR", address);
        Add("NOTE", note);
        return sb.Append(';').ToString();
    }

    /// <summary>
    /// A contact as a vCard 3.0, which carries what a MeCard cannot — an organisation and a job
    /// title — at the price of a larger code. Lines end in CR LF, as the format says.
    /// </summary>
    public static string VCard(string givenName, string? familyName = null, string? organisation = null, string? title = null,
        string? phone = null, string? email = null, string? url = null, string? address = null, string? note = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(givenName);
        var lines = new List<string>
        {
            "BEGIN:VCARD",
            "VERSION:3.0",
            "N:" + EscapeText(familyName ?? string.Empty) + ";" + EscapeText(givenName) + ";;;",
            "FN:" + EscapeText(string.Join(' ', new[] { givenName, familyName }.Where(p => !string.IsNullOrEmpty(p)))),
        };
        void Add(string field, string? value)
        {
            if (!string.IsNullOrEmpty(value)) lines.Add(field + ":" + EscapeText(value));
        }

        Add("ORG", organisation);
        Add("TITLE", title);
        Add("TEL;TYPE=CELL", phone);
        Add("EMAIL", email);
        Add("URL", url);
        if (!string.IsNullOrEmpty(address)) lines.Add("ADR;TYPE=WORK:;;" + EscapeText(address));
        Add("NOTE", note);
        lines.Add("END:VCARD");
        return string.Join("\r\n", lines);
    }

    /// <summary>
    /// A place: <c>geo:52.52,13.405</c> (RFC 5870). A phone offers to show it in its maps.
    /// </summary>
    /// <param name="latitude">Degrees north, −90 to 90.</param>
    /// <param name="longitude">Degrees east, −180 to 180.</param>
    /// <param name="uncertaintyMetres">How far off the place may be, when it is not exact.</param>
    /// <exception cref="ArgumentOutOfRangeException">A coordinate is not on the earth.</exception>
    public static string Geo(double latitude, double longitude, double? uncertaintyMetres = null)
    {
        if (double.IsNaN(latitude) || latitude is < -90 or > 90) throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude is from −90 to 90.");
        if (double.IsNaN(longitude) || longitude is < -180 or > 180) throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude is from −180 to 180.");
        var text = "geo:" + Coordinate(latitude) + "," + Coordinate(longitude);
        if (uncertaintyMetres is { } u)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(u);
            text += ";u=" + u.ToString("0.###", CultureInfo.InvariantCulture);
        }

        return text;
    }

    /// <summary>
    /// A calendar event as an iCalendar <c>VEVENT</c>, which a phone offers to add to its calendar.
    /// A timed event is written in UTC; <paramref name="allDay"/> writes the dates only, the end
    /// date being the first day after the event, as the format has it. Lines end in CR LF.
    /// </summary>
    /// <param name="summary">The event's title.</param>
    /// <param name="start">When it starts.</param>
    /// <param name="end">When it ends; after <paramref name="start"/>.</param>
    /// <param name="location">Where.</param>
    /// <param name="description">More about it.</param>
    /// <param name="allDay">An event of whole days: only the dates of <paramref name="start"/> and <paramref name="end"/> are used, and the last day is included.</param>
    /// <exception cref="ArgumentException"><paramref name="end"/> is before <paramref name="start"/>.</exception>
    public static string Event(string summary, DateTimeOffset start, DateTimeOffset end, string? location = null,
        string? description = null, bool allDay = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        if (end < start) throw new ArgumentException("An event ends after it starts.", nameof(end));
        var lines = new List<string> { "BEGIN:VCALENDAR", "VERSION:2.0", "BEGIN:VEVENT", "SUMMARY:" + EscapeText(summary) };
        if (allDay)
        {
            lines.Add("DTSTART;VALUE=DATE:" + start.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            lines.Add("DTEND;VALUE=DATE:" + end.AddDays(1).ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        }
        else
        {
            lines.Add("DTSTART:" + start.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture));
            lines.Add("DTEND:" + end.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrEmpty(location)) lines.Add("LOCATION:" + EscapeText(location));
        if (!string.IsNullOrEmpty(description)) lines.Add("DESCRIPTION:" + EscapeText(description));
        lines.Add("END:VEVENT");
        lines.Add("END:VCALENDAR");
        return string.Join("\r\n", lines);
    }

    private static string Coordinate(double v) => v.ToString("0.#######", CultureInfo.InvariantCulture);

    /// <summary>Backslash before <c>\ ; , : "</c>, the escapes of the Wi-Fi format.</summary>
    private static string EscapeWiFi(string value) => EscapeEach(value, "\\;,:\"");

    /// <summary>Backslash before <c>\ ; : ,</c>, the escapes of MeCard.</summary>
    private static string EscapeMeCard(string value) => EscapeEach(value, "\\;:,");

    private static string EscapeEach(string value, string special)
    {
        var sb = new StringBuilder(value.Length + 4);
        foreach (var c in value)
        {
            if (special.Contains(c, StringComparison.Ordinal)) sb.Append('\\');
            sb.Append(c);
        }

        return sb.ToString();
    }

    /// <summary>The text escapes of vCard and iCalendar: <c>\ ; ,</c> and line breaks.</summary>
    private static string EscapeText(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace(";", "\\;", StringComparison.Ordinal)
        .Replace(",", "\\,", StringComparison.Ordinal)
        .Replace("\r\n", "\\n", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal);

    private static string Digits(string number, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number, name);
        var sb = new StringBuilder(number.Length);
        foreach (var c in number.Trim())
        {
            if (char.IsAsciiDigit(c) || (c == '+' && sb.Length == 0) || c == '*' || c == '#') sb.Append(c);
            else if (c is ' ' or '-' or '.' or '(' or ')') continue;
            else throw new ArgumentException("A phone number is digits, with a leading + for the country.", name);
        }

        if (sb.Length == 0) throw new ArgumentException("A phone number has digits.", name);
        return sb.ToString();
    }

    /// <summary>An address in a <c>mailto:</c>: its characters left as they are but those a URI cannot hold.</summary>
    private static string MailAddress(string address) => Uri.EscapeDataString(address.Trim())
        .Replace("%40", "@", StringComparison.Ordinal);

    private static string Percent(string value) => Uri.EscapeDataString(value).Replace("%40", "@", StringComparison.Ordinal);
}
