using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using AuswertungPro.Next.Application.Dossiers;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Dreht das Planbild eines Dossiers.
///
/// Gedreht wird die DATEI, nicht eine gespeicherte Gradzahl: dann stimmt der
/// Plan ueberall — in der Vorschau, im Word und in jedem PDF, das daraus
/// entsteht. Eine Gradzahl muesste jede dieser Stellen erneut auswerten, und
/// eine davon vergisst es.
///
/// Ein Bild, das nicht im Dossierordner liegt, gehoert dem Benutzer. Es wird
/// deshalb zuerst kopiert; das Original bleibt unangetastet.
/// </summary>
public sealed class PlanImageAdjuster : IPlanImageAdjuster
{
    public PlanImageResult Rotate(string? imagePath, string targetFolder, int degrees)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            return PlanImageResult.Failed("Es ist kein Planbild gewählt.");

        if (string.IsNullOrWhiteSpace(targetFolder))
            return PlanImageResult.Failed("Es ist kein Zielordner bekannt.");

        var winkel = ((degrees % 360) + 360) % 360;
        if (winkel is not (90 or 180 or 270))
            return PlanImageResult.Failed("Gedreht wird in Vierteldrehungen.");

        try
        {
            var quelle = new BitmapImage();
            quelle.BeginInit();
            quelle.CacheOption = BitmapCacheOption.OnLoad;
            quelle.UriSource = new Uri(Path.GetFullPath(imagePath), UriKind.Absolute);
            quelle.EndInit();
            quelle.Freeze();

            var gedreht = new TransformedBitmap(quelle, new RotateTransform(winkel));
            gedreht.Freeze();

            Directory.CreateDirectory(targetFolder);
            var ziel = Zielpfad(imagePath, targetFolder);

            // Erst in eine Nebendatei schreiben: bricht das Speichern ab, bleibt
            // das bisherige Bild unversehrt.
            var zwischen = ziel + ".neu";

            var kodierer = new PngBitmapEncoder();
            kodierer.Frames.Add(BitmapFrame.Create(gedreht));

            using (var strom = new FileStream(zwischen, FileMode.Create, FileAccess.Write))
                kodierer.Save(strom);

            File.Move(zwischen, ziel, overwrite: true);
            return PlanImageResult.Ok(ziel);
        }
        catch (Exception ex)
        {
            return PlanImageResult.Failed("Der Plan konnte nicht gedreht werden: " + ex.Message);
        }
    }

    /// <summary>
    /// Liegt das Bild schon im Dossierordner, wird es ersetzt. Sonst entsteht
    /// dort eine Kopie — ein fremdes Bild wird nie veraendert.
    /// </summary>
    private static string Zielpfad(string imagePath, string targetFolder)
    {
        var ordner = Path.GetDirectoryName(Path.GetFullPath(imagePath));

        if (string.Equals(
                Path.GetFullPath(targetFolder).TrimEnd(Path.DirectorySeparatorChar),
                ordner?.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFullPath(imagePath);
        }

        var name = Path.GetFileNameWithoutExtension(imagePath);
        foreach (var zeichen in Path.GetInvalidFileNameChars())
            name = name.Replace(zeichen, '_');

        var kandidat = Path.Combine(targetFolder, name + ".png");
        var lauf = 2;

        while (File.Exists(kandidat))
        {
            kandidat = Path.Combine(targetFolder, $"{name} ({lauf}).png");
            lauf++;
        }

        return kandidat;
    }
}
