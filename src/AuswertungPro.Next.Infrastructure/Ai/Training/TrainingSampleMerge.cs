using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Hilfsmethoden fuer den In-Place-Update von TrainingSamples beim MergeOrUpdate-Pfad.
/// Kapselt die Feldkopier-Logik aus TrainingSamplesStore, damit sie isoliert testbar ist.
/// </summary>
public static class TrainingSampleMerge
{
    /// <summary>
    /// Uebernimmt alle aktualisierbaren Felder von <paramref name="source"/> in <paramref name="target"/>.
    /// Anreicherungsfelder (SourceType, TechniqueGrade, InspectionDate) werden nur gesetzt wenn der
    /// neue Wert nicht null ist. BBox wird nur uebernommen wenn die Quelle eine vollstaendige Box hat.
    /// </summary>
    public static void ApplyUpdatableFields(TrainingSample target, TrainingSample source)
    {
        target.Status = source.Status;
        target.Notes = source.Notes;
        target.MatchLevel = source.MatchLevel;
        target.KiCode = source.KiCode;
        target.KbIndexState = source.KbIndexState;
        if (source.CodeMeta is not null)
            target.CodeMeta = GroundTruthProtocolEntryMapper.CloneCodeMeta(source.CodeMeta);
        // Anreicherung: nur ueberschreiben wenn der neue Wert gesetzt ist
        if (source.SourceType is not null) target.SourceType = source.SourceType;
        if (source.TechniqueGrade is not null) target.TechniqueGrade = source.TechniqueGrade;
        if (source.InspectionDate is not null) target.InspectionDate = source.InspectionDate;
        if (source.InspectionDate is not null ||
            source.TrainingEligible ||
            !string.IsNullOrWhiteSpace(source.TrainingEligibilityReason))
        {
            target.TrainingEligible = source.TrainingEligible;
            target.TrainingEligibilityReason = source.TrainingEligibilityReason;
        }
        // BBox nur uebernehmen, wenn die Quelle eine vollstaendige Box hat — eine im Review gezogene
        // Box ueberlebt so spaetere Status-/KB-Updates; ohne Quelle-Box bleibt eine vorhandene erhalten.
        if (source.HasBbox)
        {
            target.BboxXCenter = source.BboxXCenter;
            target.BboxYCenter = source.BboxYCenter;
            target.BboxWidth = source.BboxWidth;
            target.BboxHeight = source.BboxHeight;
        }
    }
}
