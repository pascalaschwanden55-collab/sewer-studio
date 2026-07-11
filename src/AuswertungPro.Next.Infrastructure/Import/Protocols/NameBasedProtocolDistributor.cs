using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Common;

namespace AuswertungPro.Next.Infrastructure.Import.Protocols;

/// <summary>
/// Verteilt Protokoll-PDFs name-basiert (siehe <see cref="ProtocolNameResolver"/>): Haltungen werden
/// per (normalisiertem) Haltungsnamen gematcht — auch bei vertauschter Schacht-Reihenfolge —, Schächte
/// per Schachtnummer; fehlt der Schacht, wird er angelegt (Protokoll ist maßgebend). Nicht zuordenbare
/// PDFs landen im Report unter „nicht zugeordnet". Idempotent: gleiche Zieldatei wird nicht dupliziert.
/// </summary>
public sealed class NameBasedProtocolDistributor : INameBasedProtocolDistributor
{
    public ProtocolDistributionReport Distribute(Project project, string projectFolder, string sourceFolder, object? collectionLock = null)
    {
        int haltung = 0, schacht = 0, angelegt = 0;
        var nichtZugeordnet = new List<string>();
        var meldungen = new List<string>();

        if (!Directory.Exists(sourceFolder))
            return new ProtocolDistributionReport(0, 0, 0, nichtZugeordnet, new[] { $"Quellordner fehlt: {sourceFolder}" });

        var pdfs = Directory.EnumerateFiles(sourceFolder, "*.pdf", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);

        foreach (var pdf in pdfs)
        {
            var target = ProtocolNameResolver.Resolve(pdf);
            if (target is null)
                continue; // Nicht-Protokoll -> stillschweigend überspringen

            try
            {
                if (target.Value.Kind == ProtocolKind.Haltung)
                {
                    var rec = FindHaltung(project, target.Value.Name);
                    if (rec is null) { nichtZugeordnet.Add(Path.GetFileName(pdf)); continue; }
                    var name = rec.GetFieldValue(FieldKeys.HoldingName) ?? target.Value.Name;
                    var dest = CopyInto(ProjectStructure.HaltungVerteiltDir(projectFolder, ProjectPathResolver.SanitizePathSegment(name)), pdf);
                    rec.SetFieldValue(FieldKeys.PdfPath, ProjectPathResolver.MakeRelative(dest, projectFolder), FieldSource.Legacy, userEdited: false);
                    haltung++;
                }
                else
                {
                    var rec = FindSchacht(project, target.Value.Name);
                    var nr = rec?.GetFieldValue("Schachtnummer") ?? target.Value.Name;
                    // Erst kopieren; erst bei Erfolg den fehlenden Schacht anlegen -> keine Geister-Schaechte
                    // (ohne PDF), falls das Kopieren scheitert.
                    var dest = CopyInto(ProjectStructure.SchachtVerteiltDir(projectFolder, ProjectPathResolver.SanitizePathSegment(nr)), pdf);
                    if (rec is null)
                    {
                        rec = new SchachtRecord();
                        rec.SetFieldValue("Schachtnummer", target.Value.Name);
                        AddSchacht(project, rec, collectionLock);
                        angelegt++;
                    }
                    rec.SetFieldValue(FieldKeys.PdfPath, ProjectPathResolver.MakeRelative(dest, projectFolder));
                    schacht++;
                }
            }
            catch (Exception ex)
            {
                meldungen.Add($"{Path.GetFileName(pdf)}: {ex.Message}");
            }
        }

        return new ProtocolDistributionReport(haltung, schacht, angelegt, nichtZugeordnet, meldungen);
    }

    private static HaltungRecord? FindHaltung(Project project, string name)
    {
        var norm = HoldingKeyNormalizer.NormalizeIbak(name);
        var rec = project.Data.FirstOrDefault(r =>
            HoldingKeyNormalizer.NormalizeIbak(r.GetFieldValue(FieldKeys.HoldingName)) == norm);
        if (rec is not null) return rec;

        // Vertauschte Schacht-Reihenfolge A-B <-> B-A (nur bei genau einem '-').
        var parts = name.Split('-');
        if (parts.Length == 2)
        {
            var reversed = HoldingKeyNormalizer.NormalizeIbak(parts[1] + "-" + parts[0]);
            rec = project.Data.FirstOrDefault(r =>
                HoldingKeyNormalizer.NormalizeIbak(r.GetFieldValue(FieldKeys.HoldingName)) == reversed);
        }
        return rec;
    }

    private static SchachtRecord? FindSchacht(Project project, string nr)
    {
        var norm = HoldingKeyNormalizer.NormalizeIbak(nr);
        return project.SchaechteData.FirstOrDefault(r =>
            HoldingKeyNormalizer.NormalizeIbak(r.GetFieldValue("Schachtnummer")) == norm);
    }

    // Fügt einen neuen Schacht threadsicher hinzu: SchaechteData ist per EnableCollectionSynchronization
    // an die UI gebunden -> Add vom Hintergrund-Thread MUSS unter dem Sync-Lock laufen, sonst
    // Cross-Thread-Fehler im Datagrid. Ohne Lock (z.B. in Tests) direkt.
    private static void AddSchacht(Project project, SchachtRecord rec, object? collectionLock)
    {
        if (collectionLock is null)
            project.SchaechteData.Add(rec);
        else
            lock (collectionLock)
                project.SchaechteData.Add(rec);
    }

    private static string CopyInto(string destDir, string sourcePdf)
    {
        Directory.CreateDirectory(destDir);
        var dest = Path.Combine(destDir, Path.GetFileName(sourcePdf));
        if (!File.Exists(dest))
            File.Copy(sourcePdf, dest, overwrite: false);
        return dest;
    }
}
