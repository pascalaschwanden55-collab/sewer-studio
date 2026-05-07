using System;

namespace AuswertungPro.Next.Application.Imaging;

/// <summary>
/// Provider-Pattern fuer den WPF-Imaging-Adapter (Phase 5.3 Sub-A).
/// Beim App-Start wird via SetDecoder eine Implementierung registriert
/// (z.B. WpfImagePixelDecoder aus der UI-Schicht). Application-Services
/// rufen Decode oder TryDecode auf ohne WPF zu kennen.
///
/// Analog zu KnowledgeRootProvider/OllamaConfigProvider/etc.
/// </summary>
public static class ImagePixelDecoderProvider
{
    private static IImagePixelDecoder? _decoder;

    /// <summary>Beim App-Start einmalig setzen.</summary>
    public static void SetDecoder(IImagePixelDecoder decoder)
    {
        ArgumentNullException.ThrowIfNull(decoder);
        _decoder = decoder;
    }

    /// <summary>
    /// Synchroner Decode. Wirft <see cref="InvalidOperationException"/> wenn
    /// kein Decoder registriert ist (Programmierfehler — sollte beim App-
    /// Start passieren).
    /// </summary>
    public static DecodedImage? Decode(byte[] imageBytes, int? maxWidth = null)
    {
        if (_decoder is null)
            throw new InvalidOperationException(
                "ImagePixelDecoderProvider: Kein Decoder registriert. " +
                "WpfImagePixelDecoder in App.xaml.cs registrieren.");
        return _decoder.DecodeBgra32(imageBytes, maxWidth);
    }

    /// <summary>
    /// Versucht zu dekodieren. Gibt null zurueck wenn entweder kein Decoder
    /// registriert ist oder das Format nicht unterstuetzt wird. Nuetzlich
    /// fuer Best-Effort-Pfade (z.B. Diagnose), die ohne Decoder leben koennen.
    /// </summary>
    public static DecodedImage? TryDecode(byte[] imageBytes, int? maxWidth = null)
    {
        try
        {
            return _decoder?.DecodeBgra32(imageBytes, maxWidth);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>True wenn ein Decoder registriert ist.</summary>
    public static bool HasDecoder => _decoder is not null;
}
