using QRCoder;

namespace InterviewPrep.Infrastructure;

/// <summary>
/// Holds the LAN address other devices (e.g. your phone on the same Wi-Fi) can
/// use to open the app, plus a scannable QR code for it. Populated once at web
/// startup so pages can show a "scan to open on your phone" card.
/// </summary>
public static class NetworkInfo
{
    /// <summary>e.g. http://192.168.1.20:5095 — null when no LAN address found.</summary>
    public static string? PhoneUrl { get; private set; }

    /// <summary>Inline SVG QR code for <see cref="PhoneUrl"/>, or null.</summary>
    public static string? QrSvg { get; private set; }

    /// <summary>Sets the phone URL and builds its QR code once at startup.</summary>
    public static void Configure(string? phoneUrl)
    {
        PhoneUrl = phoneUrl;
        QrSvg = string.IsNullOrWhiteSpace(phoneUrl) ? null : BuildQrSvg(phoneUrl);
    }

    private static string? BuildQrSvg(string text)
    {
        try
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
            // pixelsPerModule = 4 keeps the SVG compact; CSS scales it to fit.
            return new SvgQRCode(data).GetGraphic(4);
        }
        catch
        {
            return null;
        }
    }
}
