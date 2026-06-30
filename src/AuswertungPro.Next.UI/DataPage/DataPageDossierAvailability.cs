using System;
using System.IO;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

public sealed record DataPageDossierPrintableSections(
    bool HasDossierBaseSection,
    bool HasOriginalPdfSection)
{
    public bool HasAnySection => HasDossierBaseSection || HasOriginalPdfSection;
}

/// <summary>
/// Reine Verfuegbarkeitspruefung fuer das Haltungs-Dossier: stellt fest, ob
/// druckbare Foto-Pfade vorhanden sind. Keine PDF-Erzeugung, keine Dialoge.
/// </summary>
public static class DataPageDossierAvailability
{
    public static DataPageDossierPrintableSections EvaluatePrintableSections(
        DossierPrintOptions options,
        HaltungRecord record,
        string projectFolder,
        bool hasSchachtVon,
        bool hasSchachtBis,
        bool hasHydraulikResult,
        bool kostenAvailable,
        int originalPdfCount)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(record);

        var hasDossierBaseSection =
            options.IncludeDeckblatt
            || options.IncludeHaltungsprotokoll
            || (options.IncludeFotos && HasPrintablePhotos(record, projectFolder))
            || (options.IncludeSchachtVon && hasSchachtVon)
            || (options.IncludeSchachtBis && hasSchachtBis)
            || (options.IncludeHydraulik && hasHydraulikResult)
            || (options.IncludeKostenschaetzung && kostenAvailable);

        var hasOriginalPdfSection = options.IncludeOriginalProtokolle && originalPdfCount > 0;

        return new DataPageDossierPrintableSections(
            hasDossierBaseSection,
            hasOriginalPdfSection);
    }

    /// <summary>
    /// Prueft, ob die aktuelle Protokoll-Revision der Haltung mindestens ein
    /// (nicht geloeschtes) Foto enthaelt, dessen Datei tatsaechlich existiert.
    /// </summary>
    public static bool HasPrintablePhotos(HaltungRecord record, string projectFolder)
    {
        var entries = record.Protocol?.Current?.Entries;
        if (entries is null || entries.Count == 0)
            return false;

        foreach (var entry in entries)
        {
            if (entry.IsDeleted || entry.FotoPaths is null || entry.FotoPaths.Count == 0)
                continue;

            foreach (var raw in entry.FotoPaths)
            {
                var resolved = ResolveDossierPhotoPath(raw, projectFolder);
                if (!string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Loest einen Foto-Pfad auf: absolute Pfade bleiben unveraendert, relative
    /// werden gegen den Projektordner kombiniert. Leere Eingaben ergeben null.
    /// </summary>
    public static string? ResolveDossierPhotoPath(string? raw, string projectFolder)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var normalized = raw.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalized))
            return normalized;

        if (string.IsNullOrWhiteSpace(projectFolder))
            return null;

        return Path.GetFullPath(Path.Combine(projectFolder, normalized));
    }
}
