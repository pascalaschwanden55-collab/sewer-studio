using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Verteilt beim Ein-Knopf-Import je Haltung das Video (flach + datumsbenannt <c>JJJJMMTT_&lt;Haltung&gt;.mpg</c>,
/// wie „Haltung Verteilen") und generiert das programm-eigene Protokoll (mit eingebetteten Fotos, Suffix
/// <c>_E</c>) in den Haltungsordner. Beide werden als RELATIVER Pfad am Record verlinkt, damit „Video Play"
/// und „Protokoll öffnen" die verteilte Kopie nutzen. Das generierte Protokoll ist immer aktuell
/// (Haltungsnummer, DN, Befunde) — kein Original-PDF-Rewrite noetig.
///
/// WICHTIG: erst die Fotos ins Projekt verteilen (relativ, z.B. via MediaDistributionService), DANN das
/// Protokoll generieren — der Exporter bettet nur projekt-relative, existierende Fotos ein.
/// </summary>
public static class KanalImportDistributor
{
    public sealed record Result(int VideosDistributed, int ProtocolsGenerated, int Errors, IReadOnlyList<string> Messages);

    public static Result DistributeVideosAndProtocols(Project project, string projectFolder)
    {
        var messages = new List<string>();
        int videos = 0, protos = 0, errors = 0;
        var exporter = new ProtocolPdfExporter();

        foreach (var record in project.Data.ToList())
        {
            var haltung = record.GetFieldValue("Haltungsname")?.Trim();
            if (string.IsNullOrWhiteSpace(haltung))
                continue;

            var san = ProjectPathResolver.SanitizePathSegment(haltung);
            var dir = ProjectStructure.HaltungVerteiltDir(projectFolder, san);
            var stamp = ResolveDateStamp(record);

            // 1) Video flach + datumsbenannt (JJJJMMTT_<Haltung>.<ext>) + relativ verlinken.
            try
            {
                var link = record.GetFieldValue("Link")?.Trim();
                if (!string.IsNullOrWhiteSpace(link) && !ProjectPathResolver.IsRelative(link) && File.Exists(link))
                {
                    Directory.CreateDirectory(dir);
                    var ext = Path.GetExtension(link);
                    var dest = UniquePath(Path.Combine(dir, $"{stamp}_{san}{ext}"));
                    File.Copy(link, dest, overwrite: false);
                    record.SetFieldValue("Link", ProjectPathResolver.MakeRelative(dest, projectFolder), FieldSource.Legacy, userEdited: false);
                    videos++;
                }
            }
            catch (Exception ex)
            {
                errors++;
                messages.Add($"Video {haltung}: {ex.Message}");
            }

            // 2) Eigenes Protokoll generieren (mit Fotos) -> JJJJMMTT_<Haltung>_E.pdf + relativ verlinken.
            try
            {
                if (record.Protocol != null)
                {
                    var pdf = exporter.BuildHaltungsprotokollPdf(
                        project, record, record.Protocol, projectFolder,
                        new HaltungsprotokollPdfOptions { IncludePhotos = true });
                    Directory.CreateDirectory(dir);
                    var dest = Path.Combine(dir, $"{stamp}_{san}_E.pdf");
                    File.WriteAllBytes(dest, pdf);
                    record.SetFieldValue("PDF_Path", ProjectPathResolver.MakeRelative(dest, projectFolder), FieldSource.Legacy, userEdited: false);
                    protos++;
                }
            }
            catch (Exception ex)
            {
                errors++;
                messages.Add($"Protokoll {haltung}: {ex.Message}");
            }
        }

        return new Result(videos, protos, errors, messages);
    }

    // JJJJMMTT aus Datum_Jahr (verschiedene Formate), sonst "00000000".
    private static string ResolveDateStamp(HaltungRecord record)
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

    private static string UniquePath(string path)
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
