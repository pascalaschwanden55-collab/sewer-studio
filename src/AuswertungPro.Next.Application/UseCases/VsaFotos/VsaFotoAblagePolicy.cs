using System.IO;

namespace AuswertungPro.Next.Application.UseCases.VsaFotos;

/// <summary>
/// Bestimmt, wohin ein im VSA-Codierfenster aufgenommenes Befundfoto dauerhaft
/// gehoert: in den Ordner "Fotos" neben dem Video, nie in den Windows-Temp-Ordner.
/// Gleiche Ablage wie CodingSnapshotTargetPolicy im Codiermodus.
/// Ohne bekanntes Video bleibt nur der Temp-Ordner; das ist ein ehrlicher
/// Rueckfall und keine dauerhafte Ablage.
/// </summary>
public static class VsaFotoAblagePolicy
{
    public static string Ziel(
        string? videoPath,
        int photoIndex,
        DateTimeOffset now,
        Func<string, bool> existiert)
    {
        ArgumentNullException.ThrowIfNull(existiert);

        var videoOrdner = string.IsNullOrWhiteSpace(videoPath)
            ? null
            : Path.GetDirectoryName(videoPath);

        var ordner = string.IsNullOrWhiteSpace(videoOrdner)
            ? Path.GetTempPath()
            : Path.Combine(videoOrdner, "Fotos");

        var name = $"vsa_foto{photoIndex + 1}_{now:yyyyMMdd_HHmmss}";
        var pfad = Path.Combine(ordner, name + ".png");

        // Zwei Aufnahmen in derselben Sekunde duerfen sich nicht ueberschreiben.
        for (var lauf = 2; existiert(pfad) && lauf < 1000; lauf++)
            pfad = Path.Combine(ordner, $"{name}_{lauf}.png");

        return pfad;
    }
}
