using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Dossiers;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Wandelt eine Plan-PDF in ein PNG um. Verwendet den PDF-Renderer, den Windows
/// selbst mitbringt — kein zusaetzliches Paket.
///
/// Umgewandelt wird die ERSTE Seite: der Uebersichtsplan ist ein Blatt. Ein
/// mehrseitiges PDF wird deshalb ausdruecklich gemeldet, statt stillschweigend
/// nur den Anfang zu nehmen.
///
/// Das Kundenoriginal wird nur gelesen. Das Bild entsteht im uebergebenen
/// Ordner; eine vorhandene Datei wird nie ueberschrieben.
///
/// Diese Klasse liegt in der Oberflaechenschicht, weil der Windows-Renderer nur
/// dort erreichbar ist — die Infrastruktur baut plattformneutral.
/// </summary>
public sealed class WindowsPdfPlanImageConverter : IPlanImageConverter
{
    /// <summary>
    /// Breite des erzeugten Bildes. 2480 Punkte entsprechen A4 quer bei 300 dpi
    /// und reichen fuer einen lesbaren Plan im Dossier.
    /// </summary>
    private const uint Zielbreite = 2480;

    public bool NeedsConversion(string? path)
        => !string.IsNullOrWhiteSpace(path)
            && string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);

    public async Task<PlanImageResult> ConvertAsync(
        string sourcePath, string targetFolder, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return PlanImageResult.Failed("Die Plandatei wurde nicht gefunden.");

        if (string.IsNullOrWhiteSpace(targetFolder))
            return PlanImageResult.Failed("Es ist kein Zielordner bekannt.");

        try
        {
            Directory.CreateDirectory(targetFolder);

            var datei = await global::Windows.Storage.StorageFile
                .GetFileFromPathAsync(Path.GetFullPath(sourcePath))
                .AsTask(ct)
                .ConfigureAwait(true);

            var dokument = await global::Windows.Data.Pdf.PdfDocument
                .LoadFromFileAsync(datei)
                .AsTask(ct)
                .ConfigureAwait(true);

            if (dokument.PageCount == 0)
                return PlanImageResult.Failed("Die Plandatei enthält keine Seite.");

            var ziel = FreierZielpfad(targetFolder, Path.GetFileNameWithoutExtension(sourcePath));

            using (var seite = dokument.GetPage(0))
            {
                var einstellungen = new global::Windows.Data.Pdf.PdfPageRenderOptions
                {
                    DestinationWidth = Zielbreite
                };

                using var strom = new FileStream(ziel, FileMode.CreateNew, FileAccess.Write);
                using var ram = new global::Windows.Storage.Streams.InMemoryRandomAccessStream();

                await seite.RenderToStreamAsync(ram, einstellungen).AsTask(ct).ConfigureAwait(true);

                ram.Seek(0);
                await ram.AsStreamForRead().CopyToAsync(strom, ct).ConfigureAwait(true);
            }

            return dokument.PageCount > 1
                ? PlanImageResult.Ok(ziel) with
                {
                    Error = $"Die Plandatei hat {dokument.PageCount} Seiten — "
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
            // Ein nicht lesbares PDF darf das Dossier nicht verhindern; die
            // Stelle bleibt dann leer und sagt warum.
            return PlanImageResult.Failed("Die Plandatei konnte nicht umgewandelt werden: " + ex.Message);
        }
    }

    /// <summary>
    /// Ein freier Name im Zielordner. Eine vorhandene Datei wird nie
    /// ueberschrieben — sie koennte der Plan einer anderen Liegenschaft sein.
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
