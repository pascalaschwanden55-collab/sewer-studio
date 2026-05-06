// AuswertungPro – Video-Selbsttraining — Protokoll-Ladelogik
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Application.Ai.Training.Models;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training.Services;

/// <summary>
/// Factory fuer Protokoll-Laden aus verschiedenen Quellen.
/// Erstellt ein temporaeres Project, importiert, und extrahiert das ProtocolDocument.
/// Wird von VideoTrainingReviewViewModel und BenchmarkRunner genutzt.
/// </summary>
public sealed class ProtocolLoaderFactory
{
    private readonly IWinCanDbImportService? _winCan;
    private readonly IIbakImportService? _ibak;

    /// <summary>
    /// Erstellt eine ProtocolLoaderFactory. WinCan/IBAK-Services sind optional —
    /// fuer PDF-Protokolle werden sie nicht benoetigt.
    /// </summary>
    public ProtocolLoaderFactory(IWinCanDbImportService? winCan = null, IIbakImportService? ibak = null)
    {
        _winCan = winCan;
        _ibak = ibak;
    }

    /// <summary>
    /// Laedt ein Protokoll aus einer Import-Quelle.
    /// Erstellt intern ein temporaeres Project und importiert die Daten.
    /// </summary>
    /// <param name="protocolSource">Pfad zur Quelldatei (DB3, Daten.txt).</param>
    /// <param name="sourceType">ProtocolSourceTypes.WinCanDb3 oder IbakDatenTxt.</param>
    /// <param name="haltungHint">Optionaler Haltungsname oder Video-Name fuer Zuordnung.</param>
    /// <returns>ProtocolDocument der passenden Haltung, oder null bei Fehler.</returns>
    public ProtocolDocument? LoadProtocol(
        string protocolSource,
        string sourceType,
        string? haltungHint = null)
    {
        var exportRoot = Path.GetDirectoryName(protocolSource) ?? protocolSource;
        var tempProject = new Project { Name = "Loader-Temp" };
        var ctx = new ImportRunContext(CancellationToken.None, null, new ImportRunLog());

        // PDF-Protokoll: direkt parsen (kein Project-Import noetig)
        if (sourceType == ProtocolSourceTypes.InspektionsPdf)
            return LoadFromPdf(protocolSource);

        bool ok;
        switch (sourceType)
        {
            case ProtocolSourceTypes.WinCanDb3:
                if (_winCan is null) return null;
                ok = _winCan.ImportWinCanExport(exportRoot, tempProject, ctx).Ok;
                break;
            case ProtocolSourceTypes.IbakDatenTxt:
                if (_ibak is null) return null;
                ok = _ibak.ImportIbakExport(exportRoot, tempProject, ctx).Ok;
                break;
            default:
                return null;
        }

        if (!ok || tempProject.Data.Count == 0)
            return null;

        return FindBestMatch(tempProject, haltungHint);
    }

    /// <summary>
    /// Async-Wrapper fuer Verwendung als Delegate im BenchmarkRunner.
    /// </summary>
    public Task<ProtocolDocument?> LoadProtocolAsync(
        string protocolSource, string sourceType)
    {
        return Task.Run(() => LoadProtocol(protocolSource, sourceType));
    }

    /// <summary>
    /// Laedt ein Protokoll und gibt auch die zugehoerige HaltungRecord zurueck
    /// (fuer Stammdaten wie Rohrmaterial, DN, Haltungslaenge).
    /// </summary>
    public (ProtocolDocument? Protocol, HaltungRecord? Record) LoadProtocolWithRecord(
        string protocolSource,
        string sourceType,
        string? haltungHint = null)
    {
        var exportRoot = Path.GetDirectoryName(protocolSource) ?? protocolSource;
        var tempProject = new Project { Name = "Loader-Temp" };
        var ctx = new ImportRunContext(CancellationToken.None, null, new ImportRunLog());

        // PDF-Protokoll: direkt parsen, Record mit Stammdaten bauen
        if (sourceType == ProtocolSourceTypes.InspektionsPdf)
        {
            var (protocol, pdfRecord) = LoadFromPdfWithRecord(protocolSource);
            return (protocol, pdfRecord);
        }

        bool ok;
        switch (sourceType)
        {
            case ProtocolSourceTypes.WinCanDb3:
                if (_winCan is null) return (null, null);
                ok = _winCan.ImportWinCanExport(exportRoot, tempProject, ctx).Ok;
                break;
            case ProtocolSourceTypes.IbakDatenTxt:
                if (_ibak is null) return (null, null);
                ok = _ibak.ImportIbakExport(exportRoot, tempProject, ctx).Ok;
                break;
            default:
                return (null, null);
        }

        if (!ok || tempProject.Data.Count == 0)
            return (null, null);

        var record = FindBestMatchRecord(tempProject, haltungHint);
        return (record?.Protocol, record);
    }

    /// <summary>Findet die am besten passende Haltung im Projekt.</summary>
    private static ProtocolDocument? FindBestMatch(Project project, string? hint)
    {
        var record = FindBestMatchRecord(project, hint);
        return record?.Protocol;
    }

    private static HaltungRecord? FindBestMatchRecord(Project project, string? hint)
    {
        if (project.Data.Count == 0) return null;

        if (!string.IsNullOrWhiteSpace(hint))
        {
            var hintUpper = hint.ToUpperInvariant().Replace("-", "").Replace(".", "").Replace("_", "");

            // 1. Video-Link matchen
            foreach (var rec in project.Data)
            {
                var link = rec.GetFieldValue("Link");
                if (string.IsNullOrEmpty(link)) continue;
                var linkName = Path.GetFileNameWithoutExtension(link)?.ToUpperInvariant()
                    .Replace("-", "").Replace(".", "").Replace("_", "") ?? "";
                if (linkName == hintUpper || hintUpper.Contains(linkName) || linkName.Contains(hintUpper))
                    return rec;
            }

            // 2. Haltungsname matchen
            foreach (var rec in project.Data)
            {
                var name = rec.GetFieldValue("Haltungsname")?.ToUpperInvariant()
                    .Replace("-", "").Replace(".", "") ?? "";
                if (!string.IsNullOrEmpty(name) && (hintUpper.Contains(name) || name.Contains(hintUpper)))
                    return rec;
            }
        }

        // 3. Erste Haltung mit Protokoll
        return project.Data.FirstOrDefault(r => r.Protocol?.Original.Entries.Count > 0)
            ?? project.Data.FirstOrDefault();
    }

    // ═══ PDF-Protokoll laden ═══════════════════════════════════════════

    /// <summary>Laedt ein Protokoll direkt aus einer Inspektions-PDF.</summary>
    private static ProtocolDocument? LoadFromPdf(string pdfPath)
    {
        // Primaer: PdfProtocolTableParser (pdftotext-basiert, unterstuetzt alle Formate)
        var parseResult = PdfProtocolTableParser.Parse(pdfPath);

        // Fallback: PdfProtocolExtractor (PdfPig-basiert, gleiche Parsing-Logik)
        // Greift wenn pdftotext nicht installiert, Format nicht erkannt, oder
        // verschluesselte PDF (Custom Font Encoding) — PdfPig hat TryDecodeShiftedText
        if (!parseResult.HasEntries)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ProtocolLoader] PdfProtocolTableParser fand keine Eintraege in {Path.GetFileName(pdfPath)}" +
                (parseResult.Error != null ? $" ({parseResult.Error[..Math.Min(parseResult.Error.Length, 100)]})" : "") +
                " — versuche PdfProtocolExtractor Fallback...");
            var extractor = new PdfProtocolExtractor();
            IReadOnlyList<AuswertungPro.Next.Application.Ai.Training.Models.GroundTruthEntry> entries;
            try
            {
                entries = extractor.ExtractAsync(pdfPath).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ProtocolLoader] PdfProtocolExtractor Fallback FEHLGESCHLAGEN: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
            if (entries.Count > 0)
            {
                var hId = Path.GetFileNameWithoutExtension(pdfPath);
                var doc = new ProtocolDocument { HaltungId = hId };
                foreach (var e in entries)
                {
                    doc.Original.Entries.Add(new ProtocolEntry
                    {
                        Code = e.VsaCode,
                        Beschreibung = e.Text,
                        MeterStart = e.MeterStart,
                        MeterEnd = e.MeterEnd,
                        IsStreckenschaden = e.IsStreckenschaden,
                        Zeit = e.Zeit,
                        Source = ProtocolEntrySource.Imported
                    });
                }
                doc.Current.Entries.AddRange(doc.Original.Entries);
                return doc;
            }
            return null;
        }

        var haltungId = parseResult.Stammdaten.Haltungsname ?? Path.GetFileNameWithoutExtension(pdfPath);
        var protocol = new ProtocolDocument { HaltungId = haltungId };
        protocol.Original.Entries.AddRange(parseResult.Entries);
        protocol.Current.Entries.AddRange(parseResult.Entries);
        return protocol;
    }

    /// <summary>Laedt Protokoll + synthetische HaltungRecord aus PDF.</summary>
    private static (ProtocolDocument?, HaltungRecord?) LoadFromPdfWithRecord(string pdfPath)
    {
        // Primaer: PdfProtocolTableParser
        var parseResult = PdfProtocolTableParser.Parse(pdfPath);

        // Fallback: PdfProtocolExtractor
        if (!parseResult.HasEntries)
        {
            var fallbackDoc = LoadFromPdf(pdfPath);
            if (fallbackDoc is null) return (null, null);

            var fallbackRecord = new HaltungRecord { Protocol = fallbackDoc };
            fallbackRecord.SetFieldValue("Haltungsname", fallbackDoc.HaltungId ?? "", FieldSource.Pdf, false);
            return (fallbackDoc, fallbackRecord);
        }

        var stamm = parseResult.Stammdaten;
        var haltungId = stamm.Haltungsname ?? Path.GetFileNameWithoutExtension(pdfPath);

        var protocol = new ProtocolDocument { HaltungId = haltungId };
        protocol.Original.Entries.AddRange(parseResult.Entries);
        protocol.Current.Entries.AddRange(parseResult.Entries);

        // Synthetische HaltungRecord mit Stammdaten aus PDF-Header
        var record = new HaltungRecord();
        record.Protocol = protocol;
        if (!string.IsNullOrEmpty(haltungId))
            record.SetFieldValue("Haltungsname", haltungId, FieldSource.Pdf, false);
        if (!string.IsNullOrEmpty(stamm.Rohrmaterial))
            record.SetFieldValue("Rohrmaterial", stamm.Rohrmaterial, FieldSource.Pdf, false);
        if (stamm.NennweiteMm.HasValue)
            record.SetFieldValue("DN_mm", stamm.NennweiteMm.Value.ToString(), FieldSource.Pdf, false);
        if (stamm.HaltungslaengeMeter.HasValue)
            record.SetFieldValue("Haltungslaenge_m", stamm.HaltungslaengeMeter.Value.ToString("F2",
                System.Globalization.CultureInfo.InvariantCulture), FieldSource.Pdf, false);
        if (!string.IsNullOrEmpty(stamm.VideoFileName))
            record.SetFieldValue("Link", stamm.VideoFileName, FieldSource.Pdf, false);

        return (protocol, record);
    }

    // ═══ V4.2 Nachbesserung: PDF-Diagnose ════════════════════════════════

    /// <summary>
    /// Diagnostiziert warum ein PDF von den Parsern nicht erkannt wird.
    /// Wird bei Load-Fehlern aufgerufen und in den User-facing Error gehaengt,
    /// damit im Training-Center-Log sichtbar wird welche Parser-Stufe scheitert.
    /// </summary>
    public static string DiagnosePdf(string pdfPath)
    {
        var sb = new System.Text.StringBuilder();

        if (!File.Exists(pdfPath))
            return $"Datei existiert nicht: {pdfPath}";

        try
        {
            var fi = new FileInfo(pdfPath);
            sb.Append($"[{fi.Length / 1024} KB] ");
        }
        catch { /* Groesse nicht lesbar — weiter */ }

        // Stufe 1: PdfProtocolTableParser (pdftotext)
        try
        {
            var result = PdfProtocolTableParser.Parse(pdfPath);
            if (result.HasEntries)
            {
                sb.Append($"P1(pdftotext)={result.Entries.Count} Eintraege; ");
            }
            else if (!string.IsNullOrEmpty(result.Error))
            {
                var err = result.Error.Length > 120 ? result.Error[..120] + "..." : result.Error;
                sb.Append($"P1=ERR[{err}]; ");
            }
            else
            {
                sb.Append("P1=0 (Text da, Format nicht erkannt); ");
            }
        }
        catch (Exception ex)
        {
            sb.Append($"P1=EXC[{ex.GetType().Name}: {ex.Message}]; ");
        }

        // Stufe 2: PdfProtocolExtractor (PdfPig)
        try
        {
            var extractor = new PdfProtocolExtractor();
            var entries = extractor.ExtractAsync(pdfPath).GetAwaiter().GetResult();
            sb.Append($"P2(PdfPig)={entries.Count} Eintraege");
        }
        catch (Exception ex)
        {
            sb.Append($"P2=EXC[{ex.GetType().Name}: {ex.Message}]");
        }

        return sb.ToString();
    }

    // ═══ SourceType erkennen ═════════════════════════════════════════════

    /// <summary>
    /// Erkennt den SourceType automatisch aus dem Dateipfad.
    /// </summary>
    public static string DetectSourceType(string path)
    {
        if (string.IsNullOrEmpty(path)) return ProtocolSourceTypes.InspektionsPdf;

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".db3" || ext == ".sdf")
            return ProtocolSourceTypes.WinCanDb3;
        if (ext == ".pdf")
            return ProtocolSourceTypes.InspektionsPdf;

        var name = Path.GetFileName(path).ToLowerInvariant();
        if (name == "daten.txt" || ext == ".txt")
            return ProtocolSourceTypes.IbakDatenTxt;

        // Verzeichnis pruefen
        if (Directory.Exists(path))
        {
            if (Directory.GetFiles(path, "*.db3").Length > 0)
                return ProtocolSourceTypes.WinCanDb3;
            if (File.Exists(Path.Combine(path, "Daten.txt")))
                return ProtocolSourceTypes.IbakDatenTxt;
            if (Directory.GetFiles(path, "*.pdf").Length > 0)
                return ProtocolSourceTypes.InspektionsPdf;
        }

        return ProtocolSourceTypes.InspektionsPdf; // Default fuer D:\Haltungen Struktur
    }
}
