using System.Collections.Generic;
using System.IO;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.UseCases.PdfTrainingReview;

namespace AuswertungPro.Next.UI.Services;

public sealed record EvalContaminationSets(
    IReadOnlySet<string> ImageHashes,
    IReadOnlySet<string> HaltungKeys);

public static class EvalContaminationSetProvider
{
    public static EvalContaminationSets Load(AppSettings? settings)
        => Load(settings?.EvalSetRoot ?? AppSettings.Load().EvalSetRoot);

    public static EvalContaminationSets Load(string? evalSetRoot)
    {
        // Leer ist die einzige ausdrueckliche Abschaltung. Ein konfigurierter, aber
        // fehlender/defekter Schutzordner darf Training niemals still freigeben.
        // Die Regel steht seit dem Deepscan 02.10.2026 (A1/R2) an EINER Stelle.
        var sets = EvalProtectionSetReader.LoadStrict(evalSetRoot);
        return new EvalContaminationSets(sets.ImageHashes, sets.HaltungKeys);
    }

    public static TrainingPdfReviewProtectionSnapshot LoadPdfProtectionSnapshot(
        string? evalSetRoot)
    {
        // Der gemeinsame Leser verlangt bereits mindestens eine Haltungskennung (auch ein
        // Ordner nur mit Bildhashes ist ein Fehler). Hier nur der PDF-spezifische Grund dazu.
        EvalContaminationSets sets;
        try
        {
            sets = Load(evalSetRoot);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException(
                ex.Message + " PDF-Fotos dürfen ohne Haltungs-Schutz nicht importiert werden, " +
                "weil ihre Bildfarben normalisiert werden können.",
                ex);
        }

        return new TrainingPdfReviewProtectionSnapshot(
            sets.ImageHashes,
            sets.HaltungKeys);
    }
}
