using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Erzeugt am ENDE der Bearbeitung („Protokoll neu generieren") je Haltung das programm-EIGENE Protokoll
/// (mit eingebetteten Fotos, Suffix <c>_E</c>) in den Haltungsordner und verlinkt es RELATIV als
/// <c>PDF_Eigen</c>. Das ORIGINAL-Protokoll (<c>PDF_Path</c>) bleibt unangetastet — beide sind getrennt
/// öffenbar.
///
/// Das eigene Protokoll ist immer aktuell (Haltungsnummer, DN, Befunde). Der Dateiname ist fest
/// (<c>JJJJMMTT_&lt;H&gt;_E.pdf</c>) und wird bei jeder Regenerierung überschrieben, damit stets nur die
/// aktuellste Version in der Verteilung liegt.
///
/// WICHTIG: Fotos müssen bereits projekt-relativ verteilt sein (der Exporter bettet nur existierende,
/// projekt-relative Fotos ein). Beim Ein-Knopf-Import ist das nach der Medienverteilung der Fall.
/// </summary>
public static class ProtocolRegenerationService
{
    public sealed record Result(int Generated, int Errors, IReadOnlyList<string> Messages);

    /// <summary>
    /// Generiert für alle Haltungen mit Protokoll das eigene <c>_E</c>-Protokoll in die Verteilung.
    /// </summary>
    public static Result RegenerateAll(Project project, string projectFolder, ICodeCatalogProvider? codeCatalog = null)
    {
        var messages = new List<string>();
        int generated = 0, errors = 0;
        var exporter = new ProtocolPdfExporter();

        foreach (var record in project.Data.ToList())
        {
            var haltung = record.GetFieldValue("Haltungsname")?.Trim();
            if (string.IsNullOrWhiteSpace(haltung) || record.Protocol is null)
                continue;

            try
            {
                var san = ProjectPathResolver.SanitizePathSegment(haltung);
                var dir = ProjectStructure.HaltungVerteiltDir(projectFolder, san);
                Directory.CreateDirectory(dir);

                var stamp = KanalImportDistributor.ResolveDateStamp(record);
                var pdf = exporter.BuildHaltungsprotokollPdf(
                    project, record, record.Protocol, projectFolder,
                    new HaltungsprotokollPdfOptions { IncludePhotos = true, CodeCatalog = codeCatalog });

                // Fester Name -> Regenerierung überschreibt die vorherige Version (immer aktuell).
                var dest = Path.Combine(dir, $"{stamp}_{san}_E.pdf");
                File.WriteAllBytes(dest, pdf);

                record.SetFieldValue(
                    "PDF_Eigen",
                    ProjectPathResolver.MakeRelative(dest, projectFolder),
                    FieldSource.Legacy,
                    userEdited: false);
                generated++;
            }
            catch (Exception ex)
            {
                errors++;
                messages.Add($"Protokoll {haltung}: {ex.Message}");
            }
        }

        if (generated > 0)
        {
            project.ModifiedAtUtc = DateTime.UtcNow;
            project.Dirty = true;
        }

        return new Result(generated, errors, messages);
    }
}
