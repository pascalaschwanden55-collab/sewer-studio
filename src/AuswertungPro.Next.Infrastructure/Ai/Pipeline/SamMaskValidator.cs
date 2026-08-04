using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Fachliche Wahrheitspruefung fuer SAM-Masken am Gold-Gate: Eine Maske gilt nur als geprueft,
/// wenn sie nicht degradiert, formal strikt lesbar, dimensionstreu, nicht leer und zur Hand-Box
/// passend ist. Der Format-Teil (Tokens, Laufsumme, Leermaske) liegt in der Application-Schicht
/// (<see cref="SamMaskFormatValidator"/>), damit auch der Player-Codiermodus ihn nutzen kann —
/// dort liegt auch die gemeinsame 80-Prozent-Pruefung fuer Vordergrundpixel in der Hand-Box.
/// </summary>
public static class SamMaskValidator
{
    /// <summary>
    /// Prueft eine SAM-Maske gegen die manuell gezogene Box. <paramref name="reason"/> liefert
    /// bei Ungueltigkeit den deutschen Ablehnungsgrund (leer bei Gueltigkeit).
    /// </summary>
    /// <param name="rle">RLE-String der Maske (Format "start,run1,run2,...").</param>
    /// <param name="maskImageWidth">Bildbreite der Maske in Pixeln.</param>
    /// <param name="maskImageHeight">Bildhoehe der Maske in Pixeln.</param>
    /// <param name="box">Manuell gezogene Box in normalisierten Koordinaten (0..1).</param>
    /// <param name="degraded">True, wenn die Segmentierung als Teil-/Degraded-Ergebnis markiert ist.</param>
    public static bool IsValid(
        string? rle,
        int? maskImageWidth,
        int? maskImageHeight,
        BoundingBox box,
        bool degraded,
        out string reason)
    {
        if (degraded)
        {
            reason = "Maske als Degraded markiert.";
            return false;
        }

        if (!SamMaskFormatValidator.HasForegroundPixelInsideBox(
            rle,
            maskImageWidth,
            maskImageHeight,
            box,
            out reason))
        {
            return false;
        }

        return true;
    }
}
