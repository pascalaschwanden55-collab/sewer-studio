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
    private readonly IImportPdfReferenceResolver _referenzAufloeser;

    public NameBasedProtocolDistributor()
        : this(new ImportPdfReferenceResolver())
    {
    }

    public NameBasedProtocolDistributor(IImportPdfReferenceResolver referenzAufloeser)
    {
        _referenzAufloeser = referenzAufloeser ?? throw new ArgumentNullException(nameof(referenzAufloeser));
    }

    public ProtocolDistributionReport Distribute(Project project, string projectFolder, string sourceFolder, object? collectionLock = null)
        => Distribute(project, projectFolder, sourceFolder, collectionLock, fileStaging: null);

    public ProtocolDistributionReport Distribute(
        Project project,
        string projectFolder,
        string sourceFolder,
        object? collectionLock,
        IImportFileStagingSession? fileStaging)
    {
        int haltung = 0, schacht = 0, angelegt = 0;
        var nichtZugeordnet = new List<string>();
        var meldungen = new List<string>();

        if (fileStaging is null && !Directory.Exists(sourceFolder))
            return new ProtocolDistributionReport(0, 0, 0, nichtZugeordnet, new[] { $"Quellordner fehlt: {sourceFolder}" });

        var pdfs = fileStaging is null
            ? Directory.EnumerateFiles(sourceFolder, "*.pdf", SearchOption.AllDirectories)
                .Select(path => new ImportReadableFile(path, path))
                .ToList()
            : fileStaging.EnumerateReadableFiles(
                sourceFolder,
                "*.pdf",
                SearchOption.AllDirectories);

        foreach (var pdf in pdfs.OrderBy(p => p.TargetPath, StringComparer.OrdinalIgnoreCase))
        {
            var target = ProtocolNameResolver.Resolve(pdf.TargetPath)
                          ?? LoeseUeberBekannteNamen(project, pdf.TargetPath);
            if (target is null)
            {
                // Kein Bezug im Namen. Nur wenn der INHALT die Datei als TV-Protokoll
                // ausweist, ist das ein echter Verlust und muss sichtbar werden -
                // Plaene und Handbuecher bleiben still. Frueher verschwand hier jedes
                // Herstellerprotokoll unbemerkt.
                if (IstTvProtokoll(pdf.ReadPath))
                    nichtZugeordnet.Add(Path.GetFileName(pdf.TargetPath));
                continue;
            }

            try
            {
                if (target.Value.Kind == ProtocolKind.Haltung)
                {
                    var rec = FindHaltung(project, target.Value.Name);
                    if (rec is null) { nichtZugeordnet.Add(Path.GetFileName(pdf.TargetPath)); continue; }
                    var name = rec.GetFieldValue(FieldKeys.HoldingName) ?? target.Value.Name;
                    var dest = CopyInto(
                        ProjectStructure.HaltungVerteiltDir(projectFolder, ProjectPathResolver.SanitizePathSegment(name)),
                        pdf,
                        fileStaging);
                    rec.SetFieldValue(FieldKeys.PdfPath, ProjectPathResolver.MakeRelative(dest, projectFolder), FieldSource.Legacy, userEdited: false);
                    haltung++;
                }
                else
                {
                    var rec = FindSchacht(project, target.Value.Name);
                    var nr = rec?.GetFieldValue("Schachtnummer") ?? target.Value.Name;
                    // Erst kopieren; erst bei Erfolg den fehlenden Schacht anlegen -> keine Geister-Schaechte
                    // (ohne PDF), falls das Kopieren scheitert.
                    var dest = CopyInto(
                        ProjectStructure.SchachtVerteiltDir(projectFolder, ProjectPathResolver.SanitizePathSegment(nr)),
                        pdf,
                        fileStaging);
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
                meldungen.Add($"{Path.GetFileName(pdf.TargetPath)}: {ex.Message}");
            }
        }

        return new ProtocolDistributionReport(haltung, schacht, angelegt, nichtZugeordnet, meldungen);
    }

    /// <summary>
    /// Zweiter Versuch fuer Herstellernamen wie "Section_8_892037-74091.pdf": Es zaehlt
    /// nur ein Name, den das Projekt bereits kennt. Dadurch entstehen keine
    /// Geister-Haltungen aus beliebigen Zahlenfolgen im Dateinamen.
    /// </summary>
    private ProtocolTarget? LoeseUeberBekannteNamen(Project project, string pdfPfad)
    {
        var haltungsnamen = project.Data
            .Select(r => r.GetFieldValue(FieldKeys.HoldingName))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var schachtnummern = project.SchaechteData
            .Select(r => r.GetFieldValue("Schachtnummer"))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var referenz = _referenzAufloeser.Resolve(
            Path.GetFileName(pdfPfad), haltungsnamen!, schachtnummern!);

        return referenz is null
            ? null
            : new ProtocolTarget(
                referenz.Value.Kind == ImportPdfReferenceKind.Haltung
                    ? ProtocolKind.Haltung
                    : ProtocolKind.Schacht,
                referenz.Value.Name);
    }

    /// <summary>
    /// Inhaltliche Zweitmeinung fuer nicht zugeordnete PDFs. Fehler beim Lesen gelten
    /// als "kein Protokoll" - eine unlesbare Datei darf den Import nicht anhalten.
    /// </summary>
    private static bool IstTvProtokoll(string pdfPfad)
    {
        try
        {
            return PdfDokumentTypErkennung.ErkenneDatei(pdfPfad) == PdfDokumentTyp.TvProtokoll;
        }
        catch
        {
            return false;
        }
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

    private static string CopyInto(
        string destDir,
        ImportReadableFile sourcePdf,
        IImportFileStagingSession? fileStaging)
    {
        if (fileStaging is not null)
        {
            return fileStaging.StageCopyAs(
                sourcePdf.ReadPath,
                destDir,
                Path.GetFileName(sourcePdf.TargetPath));
        }

        Directory.CreateDirectory(destDir);
        var dest = Path.Combine(destDir, Path.GetFileName(sourcePdf.TargetPath));
        if (!File.Exists(dest))
            File.Copy(sourcePdf.ReadPath, dest, overwrite: false);
        return dest;
    }
}
