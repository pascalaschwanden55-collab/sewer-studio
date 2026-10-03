using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Haltungsverteilung des Training Centers: Sammel-PDF nach Haltungen aufteilen, je Haltung Ordner, JSON-Protokoll
/// und Videoverweis schreiben, alte Verweise bereinigen (aus <see cref="TrainingCenterImportService"/> verschoben,
/// Paket A 03.10.2026, ohne Verhaltensaenderung). Jedes Schreibziel laeuft ueber den Verteil-Pfadwaechter.
/// </summary>
internal sealed class TrainingCenterHaltungsverteilung
{
    private readonly Func<string, PdfTextExtraction> _pdfSeitenLesen;
    private readonly Func<string, IEnumerable<string>> _dateienImOrdner;
    private readonly Action<string>? _nachHaltungsordner;
    private readonly Func<string, FileAttributes?>? _leseAttribute;
    private readonly TrainingCenterFallDateien _fallDateien;

    public TrainingCenterHaltungsverteilung(
        Func<string, PdfTextExtraction> pdfSeitenLesen,
        Func<string, IEnumerable<string>> dateienImOrdner,
        Action<string>? nachHaltungsordner,
        Func<string, FileAttributes?>? leseAttribute,
        TrainingCenterFallDateien fallDateien)
    {
        _pdfSeitenLesen = pdfSeitenLesen;
        _dateienImOrdner = dateienImOrdner;
        _nachHaltungsordner = nachHaltungsordner;
        _leseAttribute = leseAttribute;
        _fallDateien = fallDateien;
    }

    internal TrainingCenterImportService.DistributeResult Verteile(
        string pdfPath,
        string videoFolder,
        string outputFolder,
        List<string> messages,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Deepscan R6: Der Ausgabeordner liegt neben der Kundenablage. Vor jedem Schreiben gilt der
        // gemeinsame Verteil-Pfadwaechter; ein verknuepfter Ausgabeordner wird ohne Schreiben abgelehnt.
        // PR #85: Der Waechter prueft nur ab dem Ausgabeordner abwaerts. Liegt das PDF unter einer
        // Verknuepfung, laege auch der abgeleitete Ausgabeordner darin; deshalb zuerst der ganze Pfad
        // bis zum Laufwerk (gemeinsamer VerknuepfungsSchutz, fehlender Rest erlaubt, Lesefehler sperren).
        var pfadBefund = VerknuepfungsSchutz.PruefePfadAbLaufwerk(outputFolder, VerknuepfungsRegel.GanzerPfad);
        if (!pfadBefund.IstSicher)
        {
            messages.Add($"Ausgabeordner «{outputFolder}» wird nicht beschrieben: «{pfadBefund.Pfad}» im Pfad ist "
                         + (pfadBefund.Befund == VerknuepfungsBefund.Verknuepfung
                             ? "eine Verknüpfung (Junction)."
                             : "nicht sicher prüfbar (keine Verknüpfung nachweisbar)."));
            return new TrainingCenterImportService.DistributeResult(0, 0, 0, 0, outputFolder, messages);
        }

        DistributionWritePathGuard writePaths;
        try
        {
            writePaths = new DistributionWritePathGuard(outputFolder);
        }
        catch (Exception ex) when (IstPfadwaechterAblehnung(ex))
        {
            messages.Add($"Ausgabeordner «{outputFolder}» wird nicht beschrieben: "
                         + "Er ist eine Verknüpfung (Junction) oder nicht sicher prüfbar.");
            return new TrainingCenterImportService.DistributeResult(0, 0, 0, 0, outputFolder, messages);
        }

        // 1. Text aus PDF extrahieren (seitenweise)
        PdfTextExtraction extraction;
        try
        {
            extraction = _pdfSeitenLesen(pdfPath);
        }
        catch (Exception ex)
        {
            // PR #85: kein roher Ausnahmetext; die volle Ausnahme steht im Programmlog.
            messages.Add($"PDF-Text konnte nicht extrahiert werden: {UserError.DescribeAndReport(ex, "Training Center PDF lesen")}");
            return new TrainingCenterImportService.DistributeResult(0, 0, 0, 0, outputFolder, messages);
        }

        if (extraction.Pages.Count == 0)
        {
            messages.Add("Kein Text im PDF gefunden.");
            return new TrainingCenterImportService.DistributeResult(0, 0, 0, 0, outputFolder, messages);
        }

        // 2. PDF nach Haltungen aufteilen
        var parser = new PdfParser();
        var chunks = PdfChunking.SplitIntoHaltungChunks(extraction.Pages, parser);

        if (chunks.Count == 0)
        {
            messages.Add("Keine Haltungen im PDF erkannt.");
            return new TrainingCenterImportService.DistributeResult(0, 0, 0, 0, outputFolder, messages);
        }

        // 3. Video-Index aufbauen: Haltungs-ID → Videodatei (Abbruch vor und waehrend der rekursiven Suche)
        cancellationToken.ThrowIfCancellationRequested();
        var videoIndex = TrainingCenterVideoIndex.BuildVideoIndex(videoFolder, messages, cancellationToken);

        // 4. Pro Haltung einen Ordner erstellen
        Directory.CreateDirectory(outputFolder);
        int distributed = 0;
        int videosMatched = 0;
        int uncertain = 0;

        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(chunk.DetectedId) || chunk.IsUncertain)
            {
                uncertain++;
                messages.Add($"Chunk {chunk.Index} (Seiten {chunk.PageRange}): keine Haltungs-ID erkannt, übersprungen.");
                continue;
            }

            var haltungId = chunk.DetectedId;
            var safeId = Regex.Replace(haltungId, @"[^\w\-\.]", "_");
            var caseDir = Path.Combine(outputFolder, safeId);
            var jsonPath = Path.Combine(caseDir, $"{safeId}_protokoll.json");
            if (!IstSicheresZiel(writePaths, caseDir) || !IstSicheresZiel(writePaths, jsonPath))
            {
                messages.Add($"Haltung {haltungId}: Zielordner ist eine Verknüpfung oder nicht sicher prüfbar, nicht beschrieben.");
                continue;
            }

            // Review PR #85: Ein Dateifehler einer Haltung (Ordner, Protokoll, Verweis) wird fuer diese Haltung
            // gemeldet; die Verteilung faehrt mit der naechsten fort statt ganz abzubrechen.
            List<TrainingCenterImportService.ProtocolEntry> entries;
            string? videoPath = null;
            try
            {
                Directory.CreateDirectory(caseDir);

                // JSON-Protokoll schreiben (Format kompatibel mit PdfProtocolExtractor.ExtractFromJson)
                entries = TrainingCenterProtokollJson.ExtractEntriesFromChunkText(chunk.Text);
                TrainingCenterProtokollJson.WriteProtocolJson(jsonPath, entries, haltungId, chunk.PageRange);

                // Video zuordnen
                var normalizedId = TrainingCenterVideoIndex.NormalizeId(haltungId);
                if (videoIndex.TryGetValue(normalizedId, out var matchedVideo))
                {
                    // Deepscan R6: keine symbolische Verknuepfung mehr; der Verweis auf das Originalvideo
                    // steht immer in einer .link-Datei (frueher nur der Rueckfall ohne Adminrechte).
                    // PR #85: Gezaehlt und gemeldet wird ein Video nur mit geschriebenem Verweis oder einem
                    // bereits vorhandenen Video aus frueheren Laeufen. Ein vorhandenes Ziel, das eine Verknuepfung
                    // oder nicht pruefbar ist (Symlink aelterer Laeufe), zaehlt nicht; es bleibt unberuehrt, und
                    // stattdessen wird der .link-Verweis geschrieben.
                    var videoTarget = Path.Combine(caseDir, Path.GetFileName(matchedVideo));
                    var linkPath = videoTarget + ".link";
                    if (File.Exists(videoTarget)
                        && VerknuepfungsSchutz.PruefeEintrag(videoTarget, VerknuepfungsRegel.Streng, _leseAttribute).IstSicher)
                    {
                        videoPath = videoTarget;
                    }
                    else if (!_fallDateien.PruefeVideoziel(matchedVideo).IstSicher)
                    {
                        // Derselbe Massstab wie beim Lesen des Verweises im Scan: kein Verweis hinter eine Verknuepfung.
                        messages.Add($"Haltung {haltungId}: Video «{matchedVideo}» liegt hinter einer Verknüpfung oder ist nicht "
                                     + "sicher prüfbar; kein Verweis geschrieben.");
                    }
                    else if (!File.Exists(matchedVideo))
                    {
                        // Review PR #85: Die Kettenpruefung erlaubt fehlende Pfadteile; ein seit dem Videoindex
                        // verschwundenes Video (Netzlaufwerk getrennt, geloescht) ergaebe sonst einen defekten Verweis.
                        messages.Add($"Haltung {haltungId}: Video «{matchedVideo}» ist nicht mehr vorhanden; kein Verweis geschrieben.");
                    }
                    else if (IstSicheresZiel(writePaths, linkPath))
                    {
                        AtomicTextFileWriter.WriteAllText(linkPath, matchedVideo);
                        // Nur bei eindeutiger Bereinigung gilt das Video als zugeordnet (Review PR #85).
                        if (EntferneAndereVideoverweise(writePaths, caseDir, linkPath, haltungId, messages))
                            videoPath = matchedVideo; // Original-Pfad verwenden
                        else
                            messages.Add($"Haltung {haltungId}: Videozuordnung nicht eindeutig – bitte die Verteilung erneut ausführen.");
                    }
                    else
                    {
                        messages.Add($"Haltung {haltungId}: Videoverweis ist eine Verknüpfung, nicht beschrieben.");
                    }

                    if (videoPath is not null)
                        videosMatched++;
                }
                else
                {
                    messages.Add($"Haltung {haltungId}: kein Video gefunden.");
                }
            }
            catch (Exception ex) when (IstDateifehler(ex))
            {
                messages.Add($"Haltung {haltungId}: konnte nicht geschrieben werden: "
                             + UserError.DescribeAndReport(ex, "Training Center Haltung verteilen"));
                continue;
            }

            distributed++;
            messages.Add($"Haltung {haltungId}: Seiten {chunk.PageRange}, "
                + $"{entries.Count} Beobachtungen"
                + (videoPath is not null ? $", Video: {Path.GetFileName(videoPath)}" : ""));
            _nachHaltungsordner?.Invoke(caseDir);
        }

        // Review PR #85: Ein Abbruch waehrend der letzten Haltung endet als Abbruch, nicht als «Fertig».
        cancellationToken.ThrowIfCancellationRequested();
        return new TrainingCenterImportService.DistributeResult(
            chunks.Count, distributed, videosMatched, uncertain, outputFolder, messages);
    }

    /// <summary>
    /// Prueft ein Schreibziel der Verteilung mit dem Verteil-Pfadwaechter. Eine Verknuepfung
    /// (oder ein nicht pruefbares Glied) im Ziel oder darueber bis zum Ausgabeordner sperrt.
    /// </summary>
    private static bool IstSicheresZiel(DistributionWritePathGuard writePaths, string path)
    {
        try
        {
            writePaths.EnsureFileTarget(path);
            return true;
        }
        catch (Exception ex) when (IstPfadwaechterAblehnung(ex))
        {
            return false;
        }
    }

    /// <summary>
    /// PR #85: Nach erneutem Verteilen mit anderem Video blieb der alte Verweis liegen, und der Scan konnte das
    /// veraltete Video koppeln. Entfernt werden nur andere Videoverweise der Verteilung im selben Fallordner,
    /// nie Videos; jedes Ziel ueber den Pfadwaechter. Eine Verknuepfung wird gemeldet und nicht angefasst,
    /// ein Loeschfehler gemeldet (der Scan waehlt dann bei mehreren Verweisen keinen).
    /// Rueckgabe: true, wenn danach nur noch der neue Verweis gilt. Eine verknuepfte Altdatei zaehlt nicht
    /// dagegen, weil der Scan sie ohnehin ablehnt. Bleibt ein gueltiger alter Verweis liegen oder laesst sich
    /// der Fallordner nicht auflisten, laedt der Scan den Fall ohne Video; dann darf die Verteilung keinen
    /// Videotreffer melden (Review PR #85).
    /// </summary>
    private bool EntferneAndereVideoverweise(
        DistributionWritePathGuard writePaths,
        string caseDir,
        string behalten,
        string haltungId,
        List<string> messages)
    {
        List<string> dateien;
        try
        {
            // Dieselbe Dateiliste wie der Scan; ein Auflistungsfehler bricht nicht die ganze Verteilung ab.
            dateien = _dateienImOrdner(caseDir).ToList();
        }
        catch (Exception ex) when (IstPfadwaechterAblehnung(ex))
        {
            messages.Add($"Haltung {haltungId}: alte Videoverweise konnten nicht geprüft werden: "
                         + UserError.DescribeAndReport(ex, "Training Center Videoverweise auflisten"));
            return false;
        }

        var eindeutig = true;
        foreach (var alt in dateien)
        {
            if (!_fallDateien.IstVideoverweis(alt) || string.Equals(alt, behalten, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!VerknuepfungsSchutz.PruefeEintrag(alt, VerknuepfungsRegel.Streng, _leseAttribute).IstSicher)
            {
                messages.Add($"Haltung {haltungId}: alter Videoverweis «{alt}» ist eine Verknüpfung oder nicht sicher prüfbar; "
                             + "nicht entfernt.");
                continue;
            }

            try
            {
                File.Delete(writePaths.EnsureFileTarget(alt));
                messages.Add($"Haltung {haltungId}: alter Videoverweis «{Path.GetFileName(alt)}» entfernt.");
            }
            catch (Exception ex) when (IstPfadwaechterAblehnung(ex))
            {
                eindeutig = false;
                messages.Add($"Haltung {haltungId}: alter Videoverweis «{alt}» konnte nicht entfernt werden: "
                             + UserError.DescribeAndReport(ex, "Training Center Videoverweis entfernen"));
            }
        }

        return eindeutig;
    }

    /// <summary>
    /// Dateifehler einer einzelnen Haltung; der Schreibbaustein buendelt Ersetzen und Sicherung als
    /// <see cref="AggregateException"/>, die nur aus solchen Fehlern bestehen darf (Review PR #85).
    /// </summary>
    private static bool IstDateifehler(Exception ex)
        => IstPfadwaechterAblehnung(ex)
           || ex is AggregateException sammel && sammel.InnerExceptions.Count > 0
              && sammel.InnerExceptions.All(IstPfadwaechterAblehnung);

    private static bool IstPfadwaechterAblehnung(Exception ex)
        => ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException
            or System.Security.SecurityException;
}
