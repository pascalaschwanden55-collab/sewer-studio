using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

using AuswertungPro.Next.Application.Dossiers;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Uebernimmt eine Plan-PDF oder ein Planbild als neue PNG-Datei in den
/// angegebenen Arbeitsordner.
/// Verwendet fuer PDF den Renderer, den Windows selbst mitbringt; fuer Bilder
/// genuegen die WPF-Bilddecoder. Es ist kein zusaetzliches Paket noetig.
///
/// Umgewandelt wird bei PDF die ERSTE Seite: der Uebersichtsplan ist ein Blatt.
/// Ein mehrseitiges PDF wird deshalb ausdruecklich gemeldet, statt
/// stillschweigend nur den Anfang zu nehmen.
///
/// Das Kundenoriginal wird nur gelesen. Das PNG entsteht im uebergebenen
/// Ordner; eine vorhandene Datei wird nie ueberschrieben.
///
/// Diese Klasse liegt in der Oberflaechenschicht, weil der Windows-Renderer und
/// die WPF-Bilddecoder nur dort erreichbar sind.
/// </summary>
public sealed class WindowsPdfPlanImageConverter : IPlanImageConverter
{
    /// <summary>
    /// Breite des erzeugten PDF-Bildes. 2480 Punkte entsprechen A4 quer bei
    /// 300 dpi und reichen fuer einen lesbaren Plan im Dossier.
    /// </summary>
    private const uint Zielbreite = 2480;

    /// <summary>
    /// Jede ausgewaehlte Datei muss durch diesen Dienst. So werden auch Bilder
    /// immer ins Dossier kopiert und eine nicht unterstuetzte Datei kann nicht
    /// ungeprueft bis zum Word-Export gelangen.
    /// </summary>
    public bool NeedsConversion(string? path) => !string.IsNullOrWhiteSpace(path);

    public async Task<PlanImageResult> ConvertAsync(
        string sourcePath, string targetFolder, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return PlanImageResult.Failed("Die Plandatei wurde nicht gefunden.");

        if (string.IsNullOrWhiteSpace(targetFolder))
            return PlanImageResult.Failed("Es ist kein Zielordner bekannt.");

        var erweiterung = Path.GetExtension(sourcePath);
        var istPdf = string.Equals(erweiterung, ".pdf", StringComparison.OrdinalIgnoreCase);

        if (!istPdf && !IstUnterstuetztesBild(erweiterung))
        {
            return PlanImageResult.Failed(
                "Als Plan sind PDF, PNG, JPG, JPEG und BMP erlaubt.");
        }

        string? temporaer = null;

        try
        {
            Directory.CreateDirectory(targetFolder);
            var arbeitsdatei = TemporaererZielpfad(targetFolder);
            temporaer = arbeitsdatei;

            uint seitenzahl;

            if (istPdf)
            {
                seitenzahl = await RenderePdfAsync(sourcePath, arbeitsdatei, ct)
                    .ConfigureAwait(true);
            }
            else
            {
                await Task.Run(
                        () => NormalisiereBild(sourcePath, arbeitsdatei, ct),
                        ct)
                    .ConfigureAwait(true);
                seitenzahl = 1;
            }

            var ziel = VerschiebeOhneUeberschreiben(
                arbeitsdatei,
                targetFolder,
                Path.GetFileNameWithoutExtension(sourcePath));
            temporaer = null;

            return seitenzahl > 1
                ? PlanImageResult.Ok(ziel) with
                {
                    Error = $"Die Plandatei hat {seitenzahl} Seiten – "
                        + "übernommen wurde die erste."
                }
                : PlanImageResult.Ok(ziel);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Eine nicht lesbare Datei darf das Dossier nicht verhindern; die
            // Stelle bleibt dann leer und sagt warum.
            return PlanImageResult.Failed(
                "Die Plandatei konnte nicht umgewandelt werden: " + ex.Message);
        }
        finally
        {
            LoescheTemporaereDatei(temporaer);
        }
    }

    private static bool IstUnterstuetztesBild(string? erweiterung)
        => string.Equals(erweiterung, ".png", StringComparison.OrdinalIgnoreCase)
            || string.Equals(erweiterung, ".jpg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(erweiterung, ".jpeg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(erweiterung, ".bmp", StringComparison.OrdinalIgnoreCase);

    private static async Task<uint> RenderePdfAsync(
        string sourcePath, string temporaer, CancellationToken ct)
    {
        var datei = await global::Windows.Storage.StorageFile
            .GetFileFromPathAsync(Path.GetFullPath(sourcePath))
            .AsTask(ct)
            .ConfigureAwait(true);

        var dokument = await global::Windows.Data.Pdf.PdfDocument
            .LoadFromFileAsync(datei)
            .AsTask(ct)
            .ConfigureAwait(true);

        if (dokument.PageCount == 0)
            throw new InvalidDataException("Die Plandatei enthält keine Seite.");

        using var seite = dokument.GetPage(0);
        var einstellungen = new global::Windows.Data.Pdf.PdfPageRenderOptions
        {
            DestinationWidth = Zielbreite
        };

        using var ram = new global::Windows.Storage.Streams.InMemoryRandomAccessStream();
        await seite.RenderToStreamAsync(ram, einstellungen).AsTask(ct).ConfigureAwait(true);

        ram.Seek(0);
        await using var strom = new FileStream(
            temporaer, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await ram.AsStreamForRead().CopyToAsync(strom, ct).ConfigureAwait(true);

        return dokument.PageCount;
    }

    private static void NormalisiereBild(
        string sourcePath, string temporaer, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        BitmapFrame bild;
        using (var quelle = new FileStream(
                   sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var decoder = BitmapDecoder.Create(
                quelle,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            if (decoder.Frames.Count == 0)
                throw new InvalidDataException("Die Bilddatei enthaelt kein Bild.");

            bild = BitmapFrame.Create(decoder.Frames[0]);
            bild.Freeze();
        }

        ct.ThrowIfCancellationRequested();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(bild);

        using var ziel = new FileStream(
            temporaer, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        encoder.Save(ziel);
        ct.ThrowIfCancellationRequested();
    }

    private static string TemporaererZielpfad(string targetFolder)
    {
        while (true)
        {
            var kandidat = Path.Combine(
                targetFolder,
                ".plan-import-" + Guid.NewGuid().ToString("N") + ".tmp");

            if (!File.Exists(kandidat))
                return kandidat;
        }
    }

    /// <summary>
    /// Verschiebt die vollstaendig geschriebene Temporaerdatei unter einen
    /// freien Namen. Entsteht derselbe Name gleichzeitig anderswo, wird der
    /// naechste freie Name versucht; ueberschrieben wird nie.
    /// </summary>
    private static string VerschiebeOhneUeberschreiben(
        string temporaer, string ordner, string name)
    {
        while (true)
        {
            var ziel = FreierZielpfad(ordner, name);

            try
            {
                File.Move(temporaer, ziel);
                return ziel;
            }
            catch (IOException) when (File.Exists(ziel))
            {
                // Jemand hat den Namen zwischen Pruefung und Verschieben
                // belegt. Der naechste Schleifendurchlauf waehlt einen neuen.
            }
        }
    }

    private static void LoescheTemporaereDatei(string? pfad)
    {
        if (string.IsNullOrWhiteSpace(pfad))
            return;

        try
        {
            if (File.Exists(pfad))
                File.Delete(pfad);
        }
        catch
        {
            // Ein Aufraeumfehler darf den eigentlichen Importfehler nicht
            // verdecken. Die Datei hat einen eindeutigen Temporaernamen.
        }
    }

    /// <summary>
    /// Ein freier Name im Zielordner. Eine vorhandene Datei wird nie
    /// ueberschrieben - sie koennte der Plan einer anderen Liegenschaft sein.
    /// </summary>
    private static string FreierZielpfad(string ordner, string name)
    {
        var sauber = string.IsNullOrWhiteSpace(name) ? "Uebersichtsplan" : name.Trim();

        foreach (var zeichen in Path.GetInvalidFileNameChars())
            sauber = sauber.Replace(zeichen, '_');

        var kandidat = Path.Combine(ordner, sauber + ".png");
        var lauf = 2;

        while (File.Exists(kandidat))
        {
            kandidat = Path.Combine(ordner, $"{sauber} ({lauf}).png");
            lauf++;
        }

        return kandidat;
    }
}
