using AuswertungPro.Next.Application.Ai.Teacher;

namespace AuswertungPro.Next.Application.Ai.Training.Inventory;

/// <summary>Verifiziertes Schutz-Set aus genau dem aktuellen Inventarlauf.</summary>
public sealed record TrainingInventoryProtectedSetSnapshot(
    string SetId,
    string RootPath,
    string ManifestSha256);

/// <summary>
/// Laufzeitdaten des strikten Eval-/Abnahme-Schutzes. Diese Listen werden nicht
/// als zweite Wahrheit gespeichert, sondern direkt an den ExportPlanner gereicht.
/// </summary>
public sealed record TrainingInventoryProtectionSnapshot(
    TrainingInventoryEvalProtectionStatus Status,
    IReadOnlySet<string> ImageHashes,
    IReadOnlySet<string> HoldingKeys,
    IReadOnlyList<TrainingInventoryProtectedSetSnapshot> Sets,
    string Fingerprint);

/// <summary>
/// Ein konsistenter Live-Schnappschuss: Bericht und Quelldaten stammen aus
/// demselben Lesevorgang. Dadurch kann sich zwischen Inventar und Plan nichts
/// unbemerkt veraendern.
/// </summary>
public sealed record TrainingDataInventoryRuntimeSnapshot(
    TrainingDataInventoryReport Report,
    IReadOnlyList<TeacherAnnotation> TeacherAnnotations,
    IReadOnlyList<TrainingSample> TrainingSamples,
    TrainingInventoryProtectionSnapshot Protection);
