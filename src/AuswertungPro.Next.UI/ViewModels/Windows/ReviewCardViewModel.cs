using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using TrainingMatchLevel = AuswertungPro.Next.Application.Ai.Training.MatchLevel;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

/// <summary>
/// Reine Projektion eines <see cref="ReviewQueueItem"/> auf die Karte, die dem Reviewer angezeigt wird.
/// Unveraenderliche Daten — kein INotifyPropertyChanged notwendig.
/// </summary>
public sealed class ReviewCardViewModel
{
    // Einmalig geparster MatchLevel fuer alle abgeleiteten Eigenschaften.
    private readonly TrainingMatchLevel? _level;

    /// <summary>Erstellt eine Karten-Projektion fuer den uebergebenen Kandidaten.</summary>
    public ReviewCardViewModel(ReviewQueueItem item)
    {
        FramePath     = item.SelfTrainingFramePath;
        ProtocolCode  = item.SelfTrainingVsaCode ?? "";
        Meter         = item.SelfTrainingMeter ?? 0;
        MatchLevel    = item.SelfTrainingMatchLevel ?? "";
        SampleId      = item.SelfTrainingSampleId;
        PriorityLabel = item.PriorityLabel;

        // MatchLevel-String robust in den Enum uebersetzen.
        if (Enum.TryParse<TrainingMatchLevel>(MatchLevel, ignoreCase: true, out var parsed))
            _level = parsed;

        // KI-Erkennung am Frame — bei NoFindings nichts erkannt, sonst der KI-Code.
        KiAussage = IsNoFindings ? "nichts erkannt" : (item.SelfTrainingSuggestedCode ?? "?");
    }

    /// <summary>Pfad zum Frame-Bild (kann null sein).</summary>
    public string? FramePath { get; }

    /// <summary>Dokumentierter VSA-Code aus dem Protokoll (Ground-Truth).</summary>
    public string ProtocolCode { get; }

    /// <summary>Meterstand der Fundstelle in der Haltung.</summary>
    public double Meter { get; }

    /// <summary>MatchLevel-String aus dem Self-Training-Kandidaten.</summary>
    public string MatchLevel { get; }

    /// <summary>Stabile Sample-ID fuer die spaetere Freigabe (kann null bei Altbestand sein).</summary>
    public string? SampleId { get; }

    /// <summary>Prioritaets-Beschriftung: "Hoch"/"Mittel"/"Niedrig".</summary>
    public string PriorityLabel { get; }

    /// <summary>
    /// KI-Erkennung am Frame — bei NoFindings "nichts erkannt", sonst der KI-Code.
    /// </summary>
    public string KiAussage { get; }

    /// <summary>True wenn die KI gar nichts erkannt hat (MatchLevel == NoFindings).</summary>
    public bool IsNoFindings => _level == TrainingMatchLevel.NoFindings;

    /// <summary>True wenn die KI einen Fehler gemacht hat (NoFindings oder Mismatch).</summary>
    public bool IsKiError => _level is TrainingMatchLevel.NoFindings or TrainingMatchLevel.Mismatch;

    /// <summary>Alle Kandidaten in der Queue sind zu pruefende Kandidaten.</summary>
    public string StatusLabel => "Kandidat";
}
