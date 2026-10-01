using System;
using System.Collections.Generic;
using System.IO;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Liest den Kopftext einer PDF: zuerst die eingebettete Textebene, und nur wenn es
/// gar keine gibt, die ersten Seiten per OCR.
///
/// Anlass (Buerglen 2026-09-09): Die Begleitprotokolle der Sanierung kommen als reine
/// Scans aus dem Kopierer. Der Dichtheits-Verteiler las ausschliesslich die Textebene,
/// sah nichts und nahm die Dateien gar nicht erst an. Der Schacht-Verteiler dagegen hat
/// seit jeher einen OCR-Rueckfall — deshalb landeten alle diese Protokolle beim Schacht.
///
/// OCR laeuft nur beim reinen Scan und nur ueber die ersten Seiten: Der Dokumenttyp und
/// die Haltungsangabe stehen auf dem Deckblatt.
/// </summary>
internal static class PdfDokumenttext
{
    /// <summary>Seiten, die beim reinen Scan per OCR gelesen werden.</summary>
    internal const int OcrSeiten = 2;

    internal static string? LiesKopf(string path, int maxPages = 6)
        => LiesKopf(path, maxPages, LiesSeiteMitOcr);

    internal static string? LiesKopf(string path, int maxPages, Func<string, int, string?> ocr)
    {
        var text = PdfDokumentTypErkennung.ReadPdfTextPrefix(path, maxPages);
        if (!string.IsNullOrWhiteSpace(text))
            return text;

        var teile = new List<string>();
        for (var seite = 1; seite <= OcrSeiten; seite++)
        {
            string? gelesen;
            try
            {
                gelesen = ocr(path, seite);
            }
            catch (Exception ex)
            {
                // Best effort: ein fehlendes OCR macht die Datei nur unlesbar, nicht fremd.
                AuswertungPro.Next.Application.Common.BestEffort.ReportWarning(
                    $"[PdfDokumenttext] OCR fehlgeschlagen ({path}, Seite {seite}): {ex.Message}");
                break;
            }

            if (string.IsNullOrWhiteSpace(gelesen))
                break;

            teile.Add(gelesen);
        }

        return teile.Count == 0 ? null : string.Join("\n", teile);
    }

    /// <summary>
    /// Gelesene Seiten dieses Programmlaufs, geschluesselt nach INHALT.
    ///
    /// Ein Importlauf liest dieselbe Seite mehrfach: beim Erkennen der Begleitprotokolle
    /// aus der Kundenquelle und noch einmal im Vorfilter des Schachtwegs, dort aber an
    /// der bytegleichen Kopie unter <c>Importdateien\PDF</c>. Ein Pfadschluessel wuerde
    /// das nicht bemerken. Gleiche Bytes ergeben dieselbe Texterkennung — der Inhalt ist
    /// hier der ehrlichere Schluessel und faengt zugleich eine inzwischen ausgetauschte
    /// Datei ab.
    ///
    /// Eine OCR-Seite kostet rund vier Sekunden; in Buerglen sind das 19 Dateien mit je
    /// zwei Seiten, also rund zweieinhalb Minuten Wartezeit, die sonst zweimal anfallen.
    /// </summary>
    private static readonly Dictionary<string, string?> Gelesen = new(StringComparer.Ordinal);

    /// <summary>Inhalts-Hash je Datei, damit nicht bei jeder Seite neu gehasht wird.</summary>
    private static readonly Dictionary<string, string> Hashes = new(StringComparer.Ordinal);

    private const int MaxGemerkteSeiten = 512;

    /// <summary>Der eine OCR-Weg fuer alle Importpruefungen.</summary>
    internal static string? LiesSeiteMitOcr(string path, int seite)
    {
        var schluessel = Inhaltsschluessel(path, seite);

        if (schluessel is not null)
        {
            lock (Gelesen)
            {
                if (Gelesen.TryGetValue(schluessel, out var bekannt))
                    return bekannt;
            }
        }

        var ergebnis = Pdf.PdfOcrExtractor.TryExtractPageText(path, seite);
        var text = ergebnis.Success ? ergebnis.Text : null;

        if (schluessel is not null)
        {
            lock (Gelesen)
            {
                if (Gelesen.Count >= MaxGemerkteSeiten)
                    Gelesen.Clear();
                Gelesen[schluessel] = text;
            }
        }

        return text;
    }

    /// <summary>
    /// SHA-256 der Datei plus Seitennummer. Null bei jedem Lesefehler — dann wird nur
    /// nicht gemerkt, gelesen wird trotzdem.
    /// </summary>
    private static string? Inhaltsschluessel(string path, int seite)
    {
        try
        {
            var info = new FileInfo(path);
            var dateiSchluessel = $"{info.FullName}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";

            string hash;
            lock (Hashes)
            {
                if (!Hashes.TryGetValue(dateiSchluessel, out var bekannt))
                {
                    bekannt = VerifiedImportFileCopy.ComputeSha256(path);
                    if (Hashes.Count >= MaxGemerkteSeiten)
                        Hashes.Clear();
                    Hashes[dateiSchluessel] = bekannt;
                }

                hash = bekannt;
            }

            return $"{hash}|{seite}";
        }
        catch
        {
            return null;
        }
    }
}
