using System;
using System.Collections.Generic;
using System.Globalization;
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
    private readonly IProtocolPdfDateReader _datumsleser;

    public NameBasedProtocolDistributor()
        : this(new ImportPdfReferenceResolver(), new ProtocolPdfDateReader())
    {
    }

    public NameBasedProtocolDistributor(IImportPdfReferenceResolver referenzAufloeser)
        : this(referenzAufloeser, new ProtocolPdfDateReader())
    {
    }

    public NameBasedProtocolDistributor(
        IImportPdfReferenceResolver referenzAufloeser,
        IProtocolPdfDateReader datumsleser)
    {
        _referenzAufloeser = referenzAufloeser ?? throw new ArgumentNullException(nameof(referenzAufloeser));
        _datumsleser = datumsleser ?? throw new ArgumentNullException(nameof(datumsleser));
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

        var writePathGuard = fileStaging is null
            ? new ProjectWritePathGuard(projectFolder)
            : null;

        var pdfs = fileStaging is null
            ? SafeFileEnumeration.EnumerateFilesSafe(sourceFolder, "*.pdf", recursive: true)
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
                    var sichererName = ProjectPathResolver.SanitizePathSegment(name);
                    var stempel = ImportDateStampResolver.Resolve(
                        rec.GetFieldValue("Datum_Jahr"),
                        rec.GetFieldValue(FieldKeys.PdfPath),
                        rec.GetFieldValue(FieldKeys.Link));
                    var dest = CopyInto(
                        ProjectStructure.HaltungVerteiltDir(projectFolder, sichererName),
                        pdf,
                        $"{stempel}_{sichererName}.pdf",
                        fileStaging,
                        writePathGuard);
                    rec.SetFieldValue(FieldKeys.PdfPath, ProjectPathResolver.MakeRelative(dest, projectFolder), FieldSource.Legacy, userEdited: false);
                    haltung++;
                }
                else
                {
                    var rec = FindSchacht(project, target.Value.Name);
                    var nr = rec?.GetFieldValue("Schachtnummer") ?? target.Value.Name;
                    var sichereNr = ProjectPathResolver.SanitizePathSegment(nr);
                    // Datum wie beim manuellen "Schacht verteilen": zuerst aus dem
                    // Protokoll-PDF selbst. Erst danach das Feld am Schacht - und dort
                    // bewusst NICHT "Datum/Jahr", denn das ist das Baujahr
                    // (OBJ_ConstructionDate), nicht das Pruefdatum.
                    var pdfDatum = _datumsleser.ReadSchachtDate(pdf.ReadPath);
                    var stempel = pdfDatum is not null
                        ? pdfDatum.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
                        : ImportDateStampResolver.Resolve(
                            rec?.GetFieldValue("Ausführung Datum/Jahr")
                                ?? rec?.GetFieldValue("Ausfuehrung Datum/Jahr"),
                            rec?.GetFieldValue("PDF_Path"),
                            rec?.GetFieldValue("Link"));
                    // Erst kopieren; erst bei Erfolg den fehlenden Schacht anlegen -> keine Geister-Schaechte
                    // (ohne PDF), falls das Kopieren scheitert.
                    var dest = CopyInto(
                        ProjectStructure.SchachtVerteiltDir(projectFolder, sichereNr),
                        pdf,
                        $"{stempel}_{sichereNr}.pdf",
                        fileStaging,
                        writePathGuard);
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

    /// <summary>
    /// Legt das Protokoll unter dem Namen der Verteilung ab ("JJJJMMTT_&lt;Name&gt;.pdf").
    /// Eine bereits vorhandene, inhaltsgleiche Datei wird wiederverwendet; ein
    /// abweichendes zweites Protokoll derselben Haltung bekommt einen eigenen Namen,
    /// statt still verworfen zu werden.
    /// </summary>
    private static string CopyInto(
        string destDir,
        ImportReadableFile sourcePdf,
        string zielDateiname,
        IImportFileStagingSession? fileStaging,
        ProjectWritePathGuard? writePathGuard)
    {
        if (fileStaging is not null)
        {
            // Das Staging erkennt Namenskollisionen mit abweichendem Inhalt selbst
            // und legt die Datei dann unter einem eindeutigen Namen an.
            return fileStaging.StageCopyAs(sourcePdf.ReadPath, destDir, zielDateiname);
        }

        var safeDestDir = writePathGuard!.EnsureSafeDirectoryTarget(destDir);
        Directory.CreateDirectory(safeDestDir);
        var requestedTarget = writePathGuard.EnsureSafeFileTarget(
            Path.Combine(safeDestDir, zielDateiname));
        var dest = EindeutigesZiel(requestedTarget, sourcePdf.ReadPath);
        dest = writePathGuard.EnsureSafeFileTarget(dest);
        if (!File.Exists(dest))
            File.Copy(sourcePdf.ReadPath, dest, overwrite: false);
        return dest;
    }

    /// <summary>
    /// Gleicher Inhalt -> derselbe Pfad (idempotent). Anderer Inhalt -> "_1", "_2", ...
    /// wie EnsureUniquePath in der Verteilung.
    /// </summary>
    private static string EindeutigesZiel(string wunschPfad, string quelle)
    {
        if (!File.Exists(wunschPfad) || IstInhaltsgleich(wunschPfad, quelle))
            return wunschPfad;

        var ordner = Path.GetDirectoryName(wunschPfad) ?? "";
        var stamm = Path.GetFileNameWithoutExtension(wunschPfad);
        var endung = Path.GetExtension(wunschPfad);
        for (var i = 1; i < 1000; i++)
        {
            var kandidat = Path.Combine(ordner, $"{stamm}_{i}{endung}");
            if (!File.Exists(kandidat) || IstInhaltsgleich(kandidat, quelle))
                return kandidat;
        }

        return wunschPfad;
    }

    private static bool IstInhaltsgleich(string a, string b)
    {
        try
        {
            if (new FileInfo(a).Length != new FileInfo(b).Length)
                return false;

            using var stromA = File.OpenRead(a);
            using var stromB = File.OpenRead(b);
            return System.Security.Cryptography.SHA256.HashData(stromA)
                .SequenceEqual(System.Security.Cryptography.SHA256.HashData(stromB));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Im Zweifel als verschieden behandeln - lieber eine Datei zu viel als eine verlorene.
            return false;
        }
    }
}
