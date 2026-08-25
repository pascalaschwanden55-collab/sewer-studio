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
/// Jede Bearbeitung erzeugt im angegebenen Arbeitsordner eine neue Kopie. So
/// bleiben sowohl das Kundenoriginal als auch der bisherige Plan unangetastet.
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
            var gedreht = new TransformedBitmap(Lade(imagePath), new RotateTransform(winkel));
            gedreht.Freeze();

            return Speichere(gedreht, imagePath, targetFolder);
        }
        catch (Exception ex)
        {
            return PlanImageResult.Failed("Der Plan konnte nicht gedreht werden: " + ex.Message);
        }
    }

    public PlanImageResult Crop(
        string? imagePath, string targetFolder, int x, int y, int width, int height)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            return PlanImageResult.Failed("Es ist kein Planbild gewählt.");

        if (string.IsNullOrWhiteSpace(targetFolder))
            return PlanImageResult.Failed("Es ist kein Zielordner bekannt.");

        if (width <= 0 || height <= 0)
            return PlanImageResult.Failed("Der gewählte Ausschnitt ist leer.");

        try
        {
            var quelle = Lade(imagePath);

            // Der Ausschnitt wird auf das Bild begrenzt, statt zu werfen: ein
            // Rahmen, der ueber den Rand gezogen wurde, ist eine normale
            // Handbewegung und keine Fehleingabe.
            var links = Math.Clamp(x, 0, quelle.PixelWidth - 1);
            var oben = Math.Clamp(y, 0, quelle.PixelHeight - 1);
            var breite = Math.Clamp(width, 1, quelle.PixelWidth - links);
            var hoehe = Math.Clamp(height, 1, quelle.PixelHeight - oben);

            var ausschnitt = new CroppedBitmap(
                quelle, new System.Windows.Int32Rect(links, oben, breite, hoehe));
            ausschnitt.Freeze();

            return Speichere(ausschnitt, imagePath, targetFolder);
        }
        catch (Exception ex)
        {
            return PlanImageResult.Failed("Der Plan konnte nicht zugeschnitten werden: " + ex.Message);
        }
    }

    /// <summary>
    /// Laedt das Bild frisch von der Platte.
    ///
    /// WPF merkt sich geladene Bilder nach ihrem Pfad. Ohne
    /// <see cref="BitmapCreateOptions.IgnoreImageCache"/> kaeme nach dem ersten
    /// Drehen wieder das ALTE Bild zurueck — die Datei waere richtig, die
    /// Anzeige und jede weitere Drehung falsch.
    /// </summary>
    private static BitmapImage Lade(string pfad)
    {
        var quelle = new BitmapImage();
        quelle.BeginInit();
        quelle.CacheOption = BitmapCacheOption.OnLoad;
        quelle.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        quelle.UriSource = new Uri(Path.GetFullPath(pfad), UriKind.Absolute);
        quelle.EndInit();
        quelle.Freeze();
        return quelle;
    }

    /// <summary>
    /// Schreibt das Ergebnis. Erst in eine Nebendatei: bricht das Speichern ab,
    /// bleibt das bisherige Bild unversehrt.
    /// </summary>
    private static PlanImageResult Speichere(
        BitmapSource bild, string quellpfad, string zielordner)
    {
        Directory.CreateDirectory(zielordner);
        var ziel = Zielpfad(quellpfad, zielordner);
        var zwischen = ziel + ".neu";

        var kodierer = new PngBitmapEncoder();
        kodierer.Frames.Add(BitmapFrame.Create(bild));

        using (var strom = new FileStream(zwischen, FileMode.Create, FileAccess.Write))
            kodierer.Save(strom);

        File.Move(zwischen, ziel, overwrite: true);
        return PlanImageResult.Ok(ziel);
    }

    /// <summary>
    /// Jede Bearbeitung entsteht als neue Datei im Zielordner. Das gilt
    /// auch fuer ein bereits dort liegendes Bild: Wird das Vorschaufenster
    /// verworfen, muss der bisher verwendete Plan unveraendert bleiben.
    /// </summary>
    private static string Zielpfad(string imagePath, string targetFolder)
    {
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
