using System;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Reports;

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
    private static readonly IDossierPhotoAvailabilityService DefaultService =
        new DossierPhotoFileAvailabilityService();

    internal static IDossierPhotoAvailabilityService CompatibilityService => DefaultService;

    public static DataPageDossierPrintableSections EvaluatePrintableSections(
        DossierPrintOptions options,
        HaltungRecord record,
        string projectFolder,
        bool hasSchachtVon,
        bool hasSchachtBis,
        bool hasHydraulikResult,
        bool kostenAvailable,
        int originalPdfCount)
        => EvaluatePrintableSections(
            options,
            record,
            projectFolder,
            hasSchachtVon,
            hasSchachtBis,
            hasHydraulikResult,
            kostenAvailable,
            originalPdfCount,
            DefaultService);

    internal static DataPageDossierPrintableSections EvaluatePrintableSections(
        DossierPrintOptions options,
        HaltungRecord record,
        string projectFolder,
        bool hasSchachtVon,
        bool hasSchachtBis,
        bool hasHydraulikResult,
        bool kostenAvailable,
        int originalPdfCount,
        IDossierPhotoAvailabilityService photoAvailability)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(photoAvailability);

        var hasDossierBaseSection =
            options.IncludeDeckblatt
            || options.IncludeHaltungsprotokoll
            || (options.IncludeFotos && photoAvailability.HasPrintablePhotos(record, projectFolder))
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
        => DefaultService.HasPrintablePhotos(record, projectFolder);

    /// <summary>
    /// Loest einen Foto-Pfad auf: absolute Pfade bleiben unveraendert, relative
    /// werden gegen den Projektordner kombiniert. Leere Eingaben ergeben null.
    /// </summary>
    public static string? ResolveDossierPhotoPath(string? raw, string projectFolder)
        => DossierPhotoPathResolver.Resolve(raw, projectFolder);
}
