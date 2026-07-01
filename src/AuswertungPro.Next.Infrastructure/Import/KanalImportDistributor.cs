using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Verteilt beim Ein-Knopf-Import je Haltung Video + ORIGINAL-Protokoll — mit der gleichen Logik wie
/// „Haltung Verteilen" (<see cref="HoldingFolderDistributor"/>):
///
///  1. Das Original-Protokoll-PDF (aus <c>Importdateien\PDF</c>) wird pro Haltung gesplittet und flach +
///     datumsbenannt nach <c>Haltungen_Verteilt\&lt;H&gt;\JJJJMMTT_&lt;H&gt;.pdf</c> gelegt; dabei wird auch das
///     Video gematcht (über <c>record.Link</c> aus dem XTF bzw. OSD). Das Original-Protokoll wird als
///     <c>PDF_Path</c> (RELATIV) verlinkt — das ist das Menü „Haltungsprotokoll Original (PDF) öffnen".
///  2. Alle projekt-internen absoluten Pfade (Link/PDF_Path) werden relativiert, damit „Video Play" und
///     „Protokoll öffnen" die verteilte Kopie nutzen.
///  3. Fallback: für Haltungen, deren Video der PDF-getriebene Lauf NICHT verteilt hat (z. B. wenn kein
///     Original-PDF existiert oder die Protokollseite nicht erkannt wurde), wird das Video eigenständig
///     flach + datumsbenannt kopiert. So bleibt die Video-Verteilung zuverlässig, auch wenn der PDF-Split
///     einzelne Haltungen verpasst.
///
/// Das programm-EIGENE Protokoll (Suffix <c>_E</c>) wird hier bewusst NICHT erzeugt — das übernimmt der
/// <see cref="ProtocolRegenerationService"/> am Ende der Bearbeitung („Protokoll neu generieren").
/// </summary>
public static class KanalImportDistributor
{
    public sealed record Result(
        int VideosDistributed,
        int OriginalProtocolsDistributed,
        int Errors,
        IReadOnlyList<string> Messages);

    /// <summary>
    /// Verteilt Video + Original-Protokoll je Haltung (siehe Klassen-Doku).
    /// </summary>
    /// <param name="project">Offenes Projekt (Records werden verlinkt).</param>
    /// <param name="projectFolder">Absoluter Projektstammordner.</param>
    /// <param name="archivedPdfDir">Ordner mit dem/den archivierten Original-Protokoll-PDF(s) (i. d. R. Importdateien\PDF).</param>
    /// <param name="sourceVideoDir">Quellordner, in dem die Videos liegen (Export-Root, rekursiv).</param>
    public static Result Distribute(Project project, string projectFolder, string archivedPdfDir, string sourceVideoDir)
    {
        var messages = new List<string>();
        int videos = 0, origs = 0, errors = 0;
        var destRoot = Path.Combine(projectFolder, ProjectStructure.HaltungenVerteilt);

        // 1) Original-Protokoll pro Haltung splitten + Video matchen (die echte „Haltung Verteilen"-Logik).
        //    HoldingFolderDistributor liefert bei leerem PDF-Ordner ein Fehlerergebnis (Success=false) und
        //    fasst dann kein Video an — die Videos übernimmt in diesem Fall der Fallback in Schritt 3.
        if (Directory.Exists(archivedPdfDir))
        {
            try
            {
                // NUR das maßgebliche Inspektionsprotokoll splitten (nicht alle PDFs im Ordner) —
                // sonst entstehen aus mehreren Gesamt-PDFs Duplikate (<H>.pdf + <H>_01.pdf).
                var primaryPdf = SelectPrimaryProtocolPdf(archivedPdfDir);
                var results = string.IsNullOrWhiteSpace(primaryPdf)
                    ? (IReadOnlyList<HoldingFolderDistributor.DistributionResult>)System.Array.Empty<HoldingFolderDistributor.DistributionResult>()
                    : HoldingFolderDistributor.DistributeFiles(
                        pdfFiles: new[] { primaryPdf! },
                        videoSourceFolder: sourceVideoDir,
                        destGemeindeFolder: destRoot,
                        project: project);

                foreach (var r in results)
                {
                    if (!r.Success)
                        continue;
                    if (!string.IsNullOrWhiteSpace(r.DestVideoPath))
                        videos++;
                    if (string.IsNullOrWhiteSpace(r.DestPdfPath) || string.IsNullOrWhiteSpace(r.HoldingFolder))
                        continue;

                    // Zuordnung Ergebnis -> Record über den sanitisierten Ordnernamen (wie in Export/Schacht).
                    var folderName = Path.GetFileName(
                        r.HoldingFolder!.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    var record = FindRecordBySanitizedHaltung(project, folderName);
                    if (record is null)
                        continue;

                    // PDF_Path = verteiltes ORIGINAL-Protokoll (Menü „Haltungsprotokoll Original öffnen").
                    record.SetFieldValue("PDF_Path", r.DestPdfPath!, FieldSource.Legacy, userEdited: false);
                    origs++;
                }
            }
            catch (Exception ex)
            {
                errors++;
                messages.Add($"Original-Protokoll-Verteilung: {ex.Message}");
            }
        }

        // 2) Projekt-interne absolute Pfade relativieren (HoldingFolderDistributor setzt Link absolut).
        foreach (var record in project.Data)
        {
            RelativizeIfInProject(record, "Link", projectFolder);
            RelativizeIfInProject(record, "PDF_Path", projectFolder);
        }

        // 3) Fallback-Video: Haltungen, deren Link noch auf die absolute QUELLE zeigt (nicht verteilt),
        //    bekommen ihr Video flach + datumsbenannt in den Haltungsordner + relativen Link.
        foreach (var record in project.Data.ToList())
        {
            var haltung = record.GetFieldValue("Haltungsname")?.Trim();
            if (string.IsNullOrWhiteSpace(haltung))
                continue;

            var link = record.GetFieldValue("Link")?.Trim();
            if (string.IsNullOrWhiteSpace(link) || ProjectPathResolver.IsRelative(link) || !File.Exists(link))
                continue;

            try
            {
                var san = ProjectPathResolver.SanitizePathSegment(haltung);
                var dir = ProjectStructure.HaltungVerteiltDir(projectFolder, san);
                Directory.CreateDirectory(dir);
                var stamp = ResolveDateStamp(record);
                var ext = Path.GetExtension(link);
                var dest = UniquePath(Path.Combine(dir, $"{stamp}_{san}{ext}"));
                File.Copy(link, dest, overwrite: false);
                record.SetFieldValue("Link", ProjectPathResolver.MakeRelative(dest, projectFolder), FieldSource.Legacy, userEdited: false);
                videos++;
            }
            catch (Exception ex)
            {
                errors++;
                messages.Add($"Video {haltung}: {ex.Message}");
            }
        }

        return new Result(videos, origs, errors, messages);
    }

    /// <summary>
    /// Verteilt Schacht-Protokolle: gruppiert die Seiten des maßgeblichen Protokoll-PDF nach ihrem
    /// Ober-/Unter-Schacht (jede Haltung-Seite landet im PDF ihres Ober- UND Unter-Schachts) und legt
    /// pro (echtem) Schacht ein zusammengefasstes PDF nach Schächte_Verteilt\&lt;S&gt;\ ab. Verlinkt relativ.
    /// </summary>
    public static (int Schaechte, int Errors, IReadOnlyList<string> Messages) DistributeSchachtProtocols(
        Project project, string projectFolder, string archivedPdfDir)
    {
        var messages = new List<string>();
        int schaechte = 0, errors = 0;

        var primaryPdf = SelectPrimaryProtocolPdf(archivedPdfDir);
        if (string.IsNullOrWhiteSpace(primaryPdf))
            return (0, 0, messages);

        var destRoot = Path.Combine(projectFolder, ProjectStructure.SchaechteVerteilt);
        try
        {
            var results = HoldingFolderDistributor.DistributeSchachtPagesByHaltung(
                new[] { primaryPdf! }, destRoot, project);

            foreach (var r in results)
            {
                if (!r.Success)
                {
                    errors++;
                    messages.Add(r.Message);
                    continue;
                }
                if (string.IsNullOrWhiteSpace(r.DestPdfPath) || string.IsNullOrWhiteSpace(r.HoldingFolder))
                    continue;

                var folderName = Path.GetFileName(
                    r.HoldingFolder!.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                var record = FindSchachtBySanitized(project, folderName);
                if (record is not null)
                    record.SetFieldValue("PDF_Path",
                        ProjectPathResolver.MakeRelative(r.DestPdfPath!, projectFolder));
                schaechte++;
            }
        }
        catch (Exception ex)
        {
            errors++;
            messages.Add($"Schacht-Protokoll-Verteilung: {ex.Message}");
        }

        return (schaechte, errors, messages);
    }

    // Wählt aus dem Archiv-PDF-Ordner das MASSGEBLICHE Inspektionsprotokoll: das größte PDF, das kein
    // Duplikat-Suffix (<basis>_&lt;n&gt;.pdf, wenn <basis>.pdf existiert) ist. So gewinnt das Basis-Protokoll
    // gegen Zweit-Export (_1) und den kleineren Plan. Liefert null, wenn keine PDFs vorhanden.
    internal static string? SelectPrimaryProtocolPdf(string archivedPdfDir)
    {
        if (!Directory.Exists(archivedPdfDir))
            return null;

        var pdfs = Directory.EnumerateFiles(archivedPdfDir, "*.pdf", SearchOption.TopDirectoryOnly)
            .Where(p => !Path.GetFileName(p).StartsWith("split_", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (pdfs.Count == 0)
            return null;

        var stems = new HashSet<string>(
            pdfs.Select(p => Path.GetFileNameWithoutExtension(p)), StringComparer.OrdinalIgnoreCase);

        // Ein PDF ist ein Duplikat-Variant, wenn sein Stamm = <basis>_<ziffern> und <basis>.pdf existiert.
        bool IsDuplicateVariant(string path)
        {
            var stem = Path.GetFileNameWithoutExtension(path);
            var us = stem.LastIndexOf('_');
            if (us <= 0 || us == stem.Length - 1)
                return false;
            var suffix = stem[(us + 1)..];
            if (!suffix.All(char.IsDigit))
                return false;
            var basis = stem[..us];
            return stems.Contains(basis);
        }

        return pdfs
            .OrderByDescending(p => !IsDuplicateVariant(p))                 // Nicht-Duplikate zuerst
            .ThenByDescending(p => { try { return new FileInfo(p).Length; } catch { return 0L; } })
            .ThenBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static SchachtRecord? FindSchachtBySanitized(Project project, string sanitizedFolderName)
    {
        if (project.SchaechteData is null || string.IsNullOrWhiteSpace(sanitizedFolderName))
            return null;
        return project.SchaechteData.FirstOrDefault(s =>
            string.Equals(
                ProjectPathResolver.SanitizePathSegment((s.GetFieldValue("Schachtnummer") ?? s.GetFieldValue("Nr.") ?? "").Trim()),
                sanitizedFolderName,
                StringComparison.OrdinalIgnoreCase));
    }

    // Record über den sanitisierten Haltungsnamen (== Ordnername) finden.
    private static HaltungRecord? FindRecordBySanitizedHaltung(Project project, string sanitizedFolderName)
    {
        if (string.IsNullOrWhiteSpace(sanitizedFolderName))
            return null;

        return project.Data.FirstOrDefault(x =>
            string.Equals(
                ProjectPathResolver.SanitizePathSegment((x.GetFieldValue("Haltungsname") ?? "").Trim()),
                sanitizedFolderName,
                StringComparison.OrdinalIgnoreCase));
    }

    // Relativiert ein Pfadfeld NUR, wenn es absolut ist UND unter dem Projektordner liegt (verteilte
    // Kopie). Absolute Quellpfade (außerhalb des Projekts) bleiben unverändert, damit der Fallback greift.
    private static void RelativizeIfInProject(HaltungRecord record, string field, string projectFolder)
    {
        var val = record.GetFieldValue(field)?.Trim();
        if (string.IsNullOrWhiteSpace(val) || ProjectPathResolver.IsRelative(val))
            return;

        string full, root;
        try
        {
            full = Path.GetFullPath(val);
            root = Path.GetFullPath(projectFolder);
        }
        catch
        {
            return;
        }

        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return;

        record.SetFieldValue(field, ProjectPathResolver.MakeRelative(full, projectFolder), FieldSource.Legacy, userEdited: false);
    }

    // JJJJMMTT aus Datum_Jahr (verschiedene Formate), sonst "00000000".
    internal static string ResolveDateStamp(HaltungRecord record)
    {
        var raw = record.GetFieldValue("Datum_Jahr")?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return "00000000";

        if (DateTime.TryParse(raw, CultureInfo.GetCultureInfo("de-CH"), DateTimeStyles.None, out var d)
            || DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
            return d.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length == 4)          // reines Jahr
            return digits + "0101";
        if (digits.Length >= 8)          // bereits JJJJMMTT o.ae.
            return digits.Substring(0, 8);
        return "00000000";
    }

    internal static string UniquePath(string path)
    {
        if (!File.Exists(path))
            return path;

        var dir = Path.GetDirectoryName(path) ?? "";
        var stem = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (var i = 1; i < 1000; i++)
        {
            var cand = Path.Combine(dir, $"{stem}_{i}{ext}");
            if (!File.Exists(cand))
                return cand;
        }
        return path;
    }
}
