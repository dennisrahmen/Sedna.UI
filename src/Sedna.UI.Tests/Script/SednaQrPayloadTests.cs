using System.Globalization;

namespace Sedna.UI.Tests;

/// <summary>
/// <see cref="SednaQrPayload"/>: the standard payload strings, exactly as a phone reads them, and
/// every escape that keeps one field from ending another.
/// </summary>
public class SednaQrPayloadTests
{
    [Fact]
    public void A_wifi_network_is_escaped_and_typed()
    {
        Assert.Equal("WIFI:T:WPA;S:Guest network;P:correct horse;;", SednaQrPayload.WiFi("Guest network", "correct horse"));
        Assert.Equal("WIFI:T:WPA;S:a\\;b\\:c\\,d\\\\e\\\"f;P:p\\;w;;", SednaQrPayload.WiFi("a;b:c,d\\e\"f", "p;w"));
        Assert.Equal("WIFI:T:nopass;S:Lobby;;", SednaQrPayload.WiFi("Lobby"));
        Assert.Equal("WIFI:T:SAE;S:Lab;P:pw;H:true;;", SednaQrPayload.WiFi("Lab", "pw", SednaWiFiSecurity.Wpa3, hidden: true));
        Assert.Equal("WIFI:T:WEP;S:Old;P:12345;;", SednaQrPayload.WiFi("Old", "12345", SednaWiFiSecurity.Wep));
    }

    [Fact]
    public void A_wifi_network_needs_what_its_security_needs()
    {
        Assert.Throws<ArgumentException>(() => SednaQrPayload.WiFi("Lab", "pw", SednaWiFiSecurity.None));
        Assert.Throws<ArgumentException>(() => SednaQrPayload.WiFi("Lab", null, SednaWiFiSecurity.Wpa));
        Assert.Throws<ArgumentException>(() => SednaQrPayload.WiFi(""));
    }

    [Fact]
    public void An_email_is_a_mailto_with_encoded_fields()
    {
        Assert.Equal("mailto:support@example.com", SednaQrPayload.Email("support@example.com"));
        Assert.Equal(
            "mailto:support@example.com?subject=Pairing%20help&body=Line%20one%0D%0AA%20%26%20B",
            SednaQrPayload.Email("support@example.com", "Pairing help", "Line one\r\nA & B"));
        Assert.Equal("mailto:a%2Bb@example.com?cc=c@example.com", SednaQrPayload.Email("a+b@example.com", cc: "c@example.com"));
    }

    [Fact]
    public void A_phone_number_keeps_its_digits_and_its_plus()
    {
        Assert.Equal("tel:+4930123456", SednaQrPayload.Phone("+49 (30) 123-456"));
        Assert.Equal("tel:+4930123456", SednaQrPayload.Phone("+49.30.123456"));
        Assert.Throws<ArgumentException>(() => SednaQrPayload.Phone("call me"));
        Assert.Throws<ArgumentException>(() => SednaQrPayload.Phone(" "));
    }

    [Fact]
    public void A_text_message_is_an_sms_uri()
    {
        Assert.Equal("sms:+4930123456", SednaQrPayload.Sms("+49 30 123456"));
        Assert.Equal("sms:+4930123456?body=Where%20is%20the%20order%3F", SednaQrPayload.Sms("+49 30 123456", "Where is the order?"));
    }

    [Fact]
    public void A_mecard_escapes_its_separators()
    {
        Assert.Equal(
            "MECARD:N:Fischer,Alex;TEL:+4930123456;EMAIL:alex.fischer@example.com;URL:https\\://example.com;NOTE:a\\;b\\,c;;",
            SednaQrPayload.MeCard("Alex", "Fischer", "+4930123456", "alex.fischer@example.com", "https://example.com", note: "a;b,c"));
        Assert.Equal("MECARD:N:Alex;;", SednaQrPayload.MeCard("Alex"));
    }

    [Fact]
    public void A_vcard_is_version_three_with_crlf_lines_and_escaped_text()
    {
        var card = SednaQrPayload.VCard("Alex", "Fischer", "Northwind Retail", "Dispatcher", "+4930123456", "alex.fischer@example.com",
            "https://example.com", "Dock 4; Hall 2", "Likes\nmornings, mostly");
        Assert.Equal(
            "BEGIN:VCARD\r\nVERSION:3.0\r\nN:Fischer;Alex;;;\r\nFN:Alex Fischer\r\nORG:Northwind Retail\r\nTITLE:Dispatcher\r\n"
            + "TEL;TYPE=CELL:+4930123456\r\nEMAIL:alex.fischer@example.com\r\nURL:https://example.com\r\n"
            + "ADR;TYPE=WORK:;;Dock 4\\; Hall 2\r\nNOTE:Likes\\nmornings\\, mostly\r\nEND:VCARD",
            card);
    }

    [Fact]
    public void A_place_is_a_geo_uri_in_the_invariant_culture()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal("geo:52.52,13.405", SednaQrPayload.Geo(52.52, 13.405));
            Assert.Equal("geo:-33.8688197,151.2092955;u=35", SednaQrPayload.Geo(-33.8688197, 151.2092955, 35));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => SednaQrPayload.Geo(91, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SednaQrPayload.Geo(0, 181));
        Assert.Throws<ArgumentOutOfRangeException>(() => SednaQrPayload.Geo(double.NaN, 0));
    }

    [Fact]
    public void An_event_is_a_vevent_in_utc_or_in_whole_days()
    {
        var start = new DateTimeOffset(2026, 11, 20, 9, 30, 0, TimeSpan.FromHours(1));
        var end = new DateTimeOffset(2026, 11, 20, 11, 0, 0, TimeSpan.FromHours(1));
        Assert.Equal(
            "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nBEGIN:VEVENT\r\nSUMMARY:Dock walk\\, week 47\r\nDTSTART:20261120T083000Z\r\nDTEND:20261120T100000Z\r\n"
            + "LOCATION:Dock 4\r\nDESCRIPTION:Bring a vest\r\nEND:VEVENT\r\nEND:VCALENDAR",
            SednaQrPayload.Event("Dock walk, week 47", start, end, "Dock 4", "Bring a vest"));
        Assert.Equal(
            "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nBEGIN:VEVENT\r\nSUMMARY:Stock take\r\nDTSTART;VALUE=DATE:20261120\r\nDTEND;VALUE=DATE:20261122\r\nEND:VEVENT\r\nEND:VCALENDAR",
            SednaQrPayload.Event("Stock take", start, end.AddDays(1), allDay: true));
        Assert.Throws<ArgumentException>(() => SednaQrPayload.Event("x", end, start));
    }

    [Fact]
    public void A_url_must_be_absolute()
    {
        Assert.Equal("https://example.com/pair/7Q4K", SednaQrPayload.Url(" https://example.com/pair/7Q4K "));
        Assert.Throws<ArgumentException>(() => SednaQrPayload.Url("/pair/7Q4K"));
        Assert.Throws<ArgumentException>(() => SednaQrPayload.Url(""));
    }
}
