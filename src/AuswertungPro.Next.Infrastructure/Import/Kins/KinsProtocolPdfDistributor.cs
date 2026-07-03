using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Common;

namespace AuswertungPro.Next.Infrastructure.Import.Kins;

/// <summary>
/// Verteilt die KINS-Einzelprotokoll-PDFs (…Haltung&lt;N&gt;.pdf, N = XTF-Bezeichnung)
/// aus der QUELLE nach Haltungen_Verteilt\&lt;H&gt;\JJJJMMTT_&lt;H&gt;.pdf und setzt
/// PDF_Path relativ. Bewusst aus der Quelle statt aus dem flachen Archivordner:
/// DP_Gross/DP_klein enthalten gleichnamige Dateien (Kollision beim Archivieren).
/// Idempotent: bereits relativ verlinkte, existierende PDFs werden uebersprungen.
/// </summary>
public static class KinsProtocolPdfDistributor
{
    public sealed record Result(int PdfsVerteilt, int Errors, IReadOnlyList<string> Messages);

    /// <summary>
    /// Findet das KINS-Gesamtprotokoll in der Quelle (z.B. 048473_PDF\048473_Protokoll.pdf):
    /// groesste PDF mit "Protokoll" im Namen (Deckblatt ausgeschlossen). Es ist die Basis
    /// fuer den Seiten-Split je Haltung — die automatische Wahl "groesste PDF im Archiv"
    /// wuerde bei KINS sonst Plaene/fremde PDFs treffen.
    /// </summary>
    public static string? FindeGesamtprotokoll(string sourceFolder)
    {
        try
        {
            return Infrastructure.Common.SafeFileEnumeration.EnumerateFilesSafe(sourceFolder, "*.pdf", recursive: true)
                .Where(p =>
                {
                    var name = Path.GetFileNameWithoutExtension(p);
                    return name.Contains("Protokoll", StringComparison.OrdinalIgnoreCase)
                        && !name.Contains("Deckblatt", StringComparison.OrdinalIgnoreCase);
                })
                .OrderByDescending(p => { try { return new FileInfo(p).Length; } catch { return 0L; } })
                .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public static Result Distribute(
        Project project,
        string projectFolder,
        string sourceFolder,
        IReadOnlyDictionary<string, HaltungRecord> recordsProBezeichnung)
    {
        var messages = new List<string>();
        var verteilt = 0;
        var errors = 0;

        if (recordsProBezeichnung.Count == 0 || !Directory.Exists(sourceFolder))
            return new Result(0, 0, messages);

        // Alle Quell-PDFs einmal einsammeln; DP_Gross-Treffer haben Vorrang vor DP_klein/uebrigen.
        List<string> quellPdfs;
        try
        {
            quellPdfs = Infrastructure.Common.SafeFileEnumeration.EnumerateFilesSafe(sourceFolder, "*.pdf", recursive: true)
                .OrderByDescending(p => p.Contains("DP_Gross", StringComparison.OrdinalIgnoreCase))
                .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            messages.Add($"KINS-PDF: Quellordner nicht lesbar: {ex.Message}");
            return new Result(0, 1, messages);
        }

        foreach (var (bezeichnung, record) in recordsProBezeichnung)
        {
            try
            {
                var haltung = (record.GetFieldValue(FieldKeys.HoldingName) ?? "").Trim();
                if (haltung.Length == 0)
                    continue;

                // Idempotenz: PDF_Path bereits relativ und Ziel existiert → nichts tun.
                var vorhandenerPfad = (record.GetFieldValue(FieldKeys.PdfPath) ?? "").Trim();
                if (vorhandenerPfad.Length > 0
                    && ProjectPathResolver.IsRelative(vorhandenerPfad)
                    && File.Exists(Path.Combine(projectFolder, vorhandenerPfad)))
                    continue;

                // "…Haltung<N>.pdf" exakt (Haltung1 darf nicht Haltung10 treffen).
                var muster = new Regex(
                    $@"Haltung{Regex.Escape(bezeichnung)}\.pdf$",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                var quelle = quellPdfs.FirstOrDefault(p => muster.IsMatch(Path.GetFileName(p)));
                if (quelle is null)
                {
                    messages.Add($"KINS-PDF: kein Einzelprotokoll fuer Bezeichnung '{bezeichnung}' ({haltung}) gefunden.");
                    continue;
                }

                var san = ProjectPathResolver.SanitizePathSegment(haltung);
                var zielDir = ProjectStructure.HaltungVerteiltDir(projectFolder, san);
                Directory.CreateDirectory(zielDir);
                var stamp = KanalImportDistributor.ResolveDateStamp(record);
                var ziel = Path.Combine(zielDir, $"{stamp}_{san}.pdf");

                // Wiederverwenden, wenn identische Datei schon liegt (Zweitlauf).
                if (!(File.Exists(ziel) && new FileInfo(ziel).Length == new FileInfo(quelle).Length))
                {
                    ziel = File.Exists(ziel) ? KanalImportDistributor.UniquePath(ziel) : ziel;
                    File.Copy(quelle, ziel, overwrite: false);
                }

                record.SetFieldValue(
                    FieldKeys.PdfPath,
                    ProjectPathResolver.MakeRelative(ziel, projectFolder),
                    FieldSource.Legacy,
                    userEdited: false);
                verteilt++;
            }
            catch (Exception ex)
            {
                errors++;
                messages.Add($"KINS-PDF {bezeichnung}: {ex.Message}");
            }
        }

        return new Result(verteilt, errors, messages);
    }
}
