using AuswertungPro.Next.Application.Ai.Training.ClassMaps;
using AuswertungPro.Next.Application.Ai.Training.Inventory;
using System.Text.Json.Serialization;

namespace AuswertungPro.Next.Application.Ai.Training.ExportPlans;

public enum TrainingExportSourceType
{
    TeacherAnnotation,
    TrainingSample
}

public enum TrainingExportTarget
{
    Train,
    Validation
}

public enum TrainingExportHoldingRole
{
    Train,
    DevelopmentValidation
}

public enum TrainingExportProtectedSetRole
{
    DevelopmentValidation,
    Acceptance
}

public enum TrainingExportRegistryApprovalStatus
{
    Candidate,
    Approved
}

public enum TrainingExportExclusionReason
{
    Archive,
    OriginQuarantine,
    GeometryQuarantine,
    EvaluationLocked,
    EvaluationProtectionIncomplete,
    ClassDiscarded
}

public sealed record TrainingExportSourceRef(
    TrainingExportSourceType SourceType,
    string SourceId)
{
    public string StableKey => SourceType == TrainingExportSourceType.TeacherAnnotation
        ? $"teacher:{SourceId}"
        : $"sample:{SourceId}";
}

public sealed record TrainingExportBoundingBox(
    double XCenter,
    double YCenter,
    double Width,
    double Height)
{
    public bool IsValid
    {
        get
        {
            if (!double.IsFinite(XCenter)
                || !double.IsFinite(YCenter)
                || !double.IsFinite(Width)
                || !double.IsFinite(Height)
                || Width <= 0
                || Height <= 0
                || XCenter is < 0 or > 1
                || YCenter is < 0 or > 1
                || Width > 1
                || Height > 1)
            {
                return false;
            }

            const double tolerance = 1e-9;
            return XCenter - (Width / 2) >= -tolerance
                   && YCenter - (Height / 2) >= -tolerance
                   && XCenter + (Width / 2) <= 1 + tolerance
                   && YCenter + (Height / 2) <= 1 + tolerance;
        }
    }
}

/// <summary>
/// Ein vom Live-Inventar gepruefter Quelldatensatz. Der absolute Bildpfad bleibt
/// nur im Laufzeit-Bundle und wird nie in den unveraenderlichen Plan geschrieben.
/// </summary>
public sealed record TrainingExportPlanCandidate(
    TrainingExportSourceRef Source,
    string FramePath,
    string? ImageSha256,
    string? ImageExtension,
    string? HoldingKey,
    string SourceClassKey,
    string ClassSourceKind,
    TrainingExportBoundingBox? BoundingBox,
    TrainingInventoryDisposition InventoryDisposition);

public sealed record TrainingExportProtectedSetReference(
    string SetId,
    TrainingExportProtectedSetRole Role,
    string ManifestSha256);

/// <summary>
/// Menschlich freigegebene, feste Zuordnung der Haltungen. Neue Haltungen
/// erhalten nicht automatisch einen Split, sondern sperren den Export bis zur
/// bewussten Freigabe.
/// </summary>
public sealed record TrainingExportRegistrySnapshot(
    string SchemaVersion,
    string RegistryHash,
    TrainingExportRegistryApprovalStatus ApprovalStatus,
    string? ApprovedBy,
    DateTimeOffset? ApprovedUtc,
    IReadOnlyDictionary<string, TrainingExportHoldingRole> HoldingRoles,
    IReadOnlyList<TrainingExportProtectedSetReference> ProtectedSets)
{
    public const string CurrentSchemaVersion = "1.0";

    /// <summary>
    /// Optionaler, menschlich freigegebener Pilotumfang. Leer bedeutet aus
    /// Kompatibilitaetsgruenden weiterhin: alle geeigneten TrainingSamples.
    /// </summary>
    public IReadOnlySet<string> ApprovedSampleIds { get; init; }
        = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Optionaler, menschlich kuratierter Negativ-Pool (schadensfreie Gegenbeispiele,
    /// Registry-Feld <c>negative_images</c>). Leer bei Alt-Registrys — Verhalten unveraendert.
    /// Pfade sind beim Lesen bereits vollqualifiziert.
    /// </summary>
    public IReadOnlyList<TrainingExportNegativeImage> NegativeImages { get; init; } = [];
}

/// <summary>
/// Laufzeit-Huelle des Registers. Die Schutzpfade werden nur zum Live-Pruefen
/// benutzt und nie in den dauerhaften Exportplan kopiert.
/// </summary>
public sealed record TrainingExportRegistryBundle(
    TrainingExportRegistrySnapshot Snapshot,
    IReadOnlyDictionary<string, string> ProtectedSetRootPaths);

public sealed record TrainingExportPlanRequest(
    IReadOnlyList<TrainingExportPlanCandidate> Candidates,
    TrainingYoloClassMapSnapshot ClassMap,
    TrainingExportRegistrySnapshot Registry,
    string InventoryRunId,
    IReadOnlyDictionary<string, string> SourceSnapshotHashes,
    bool EvaluationProtectionComplete,
    IReadOnlySet<string> ProtectedImageHashes,
    IReadOnlySet<string> ProtectedHoldingKeys,
    DateTimeOffset GeneratedUtc)
{
    /// <summary>
    /// Verifizierte Negativ-/Hintergrundbilder aus dem Exportregister (additive Erweiterung;
    /// leer bei bestehenden Aufrufen). Bereits gegen den Live-Bestand gehasht und auf
    /// Eval-/Abnahme-Kontamination geprueft.
    /// </summary>
    public IReadOnlyList<TrainingExportNegativeImage> NegativeImages { get; init; } = [];
}

public sealed record TrainingExportPlannedLabel(
    int ClassId,
    string ClassName,
    TrainingExportBoundingBox BoundingBox,
    IReadOnlyList<TrainingExportSourceRef> Sources);

/// <summary>Fester Kenn-Schluessel fuer kuratierte Negativbilder (kein realer Haltungsbezug).</summary>
public static class TrainingExportNegativePool
{
    /// <summary>HoldingKey, den alle Negativ-/Hintergrundbilder im Plan tragen.</summary>
    public const string HoldingKey = "negative_pool";
}

/// <summary>
/// Richtungsunabhaengige Identitaet einer Haltung. Der im Plan sichtbare
/// Schluessel behaelt seine Richtung; nur Vergleiche behandeln A-B und B-A gleich.
/// </summary>
internal static class TrainingExportHoldingIdentity
{
    public static bool IsCompleteNumericPair(string holdingKey)
    {
        var normalized = EvalContaminationGuard.NormalizeHaltungKey(holdingKey)
                         ?? holdingKey.Trim();
        var parts = normalized.Split('-', StringSplitOptions.None);
        return parts.Length == 2
               && parts.All(part =>
                   part.Length > 0
                   && part.All(character => character is >= '0' and <= '9'));
    }

    public static string PhysicalKey(string holdingKey)
    {
        var normalized = EvalContaminationGuard.NormalizeHaltungKey(holdingKey)
                         ?? holdingKey.Trim();
        var parts = normalized.Split('-', StringSplitOptions.None);
        if (parts.Length != 2
            || string.IsNullOrWhiteSpace(parts[0])
            || string.IsNullOrWhiteSpace(parts[1]))
        {
            return normalized;
        }

        return StringComparer.OrdinalIgnoreCase.Compare(parts[0], parts[1]) <= 0
            ? $"{parts[0]}|{parts[1]}"
            : $"{parts[1]}|{parts[0]}";
    }
}

/// <summary>
/// Menschlich kuratiertes Negativ-/Hintergrundbild (schadensfrei). Wird wie
/// <c>approved_sample_ids</c> im Exportregister freigegeben; optional mit Split-Hinweis —
/// ohne Hinweis entscheidet der Planer deterministisch ueber den Bild-Hash.
/// </summary>
public sealed record TrainingExportNegativeImage(
    string Path,
    string Sha256,
    TrainingExportTarget? SplitHint)
{
    /// <summary>
    /// Echte, normalisierte Haltung eines streng gebundenen Negativbilds.
    /// Null kennzeichnet den kompatiblen alten Negativ-Pool.
    /// </summary>
    public string? HoldingKey { get; init; }

    /// <summary>Richtungsunabhaengiger Schachtpaar-Schluessel, zum Beispiel 100|200.</summary>
    public string? PhysicalHoldingKey { get; init; }

    /// <summary>Erzeugertyp der strikten Registry-Zeile.</summary>
    public string? NegativeSourceType { get; init; }

    /// <summary>Eindeutige ID des menschlich freigegebenen Negativ-Sets.</summary>
    public string? NegativeSetId { get; init; }

    /// <summary>SHA-256 des eingefrorenen Negativ-Set-Manifests.</summary>
    public string? NegativeSetManifestSha256 { get; init; }

    /// <summary>ID der eingefrorenen Hard-Negative-Review-Queue.</summary>
    public string? QueueId { get; init; }

    /// <summary>SHA-256 der vollstaendigen menschlichen Review-Datei.</summary>
    public string? ReviewSha256 { get; init; }

    /// <summary>SHA-256 des eingefrorenen Review-Queue-Manifests.</summary>
    public string? QueueManifestSha256 { get; init; }

    /// <summary>SHA-256 der eingefrorenen Queue-Kandidatenliste.</summary>
    public string? CandidatesSha256 { get; init; }

    /// <summary>Version der fuer das Review gebundenen Detect-Klassenkarte.</summary>
    public int? ClassMapVersion { get; init; }

    /// <summary>SHA-256 der fuer das Review gebundenen Detect-Klassenkarte.</summary>
    public string? ClassMapSha256 { get; init; }

    /// <summary>SHA-256 des an die Klassenkarte gebundenen VSA-Manifests.</summary>
    public string? VsaManifestHash { get; init; }

    /// <summary>Bild-ID aus der menschlich geprueften Queue.</summary>
    public string? ReviewItemId { get; init; }

    /// <summary>Menschliche Freigabeentscheidung; strikt all_classes_clear.</summary>
    public string? ReviewDecision { get; init; }
}

public sealed record TrainingExportPlannedImage(
    string ImageSha256,
    string HoldingKey,
    TrainingExportTarget Target,
    string TargetFileName,
    IReadOnlyList<TrainingExportPlannedLabel> Labels)
{
    /// <summary>
    /// True fuer Negativ-/Hintergrundbilder (schadensfreie Gegenbeispiele mit bewusst
    /// leerer Labeldatei). Additiv: bei Positivem (false) wird das Feld NICHT
    /// serialisiert — bestehende Manifest-Bytes und die goldene Fixture bleiben identisch.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsNegative { get; init; }
}

public sealed record TrainingExportExclusion(
    TrainingExportSourceRef Source,
    TrainingExportExclusionReason Reason);

/// <summary>
/// Dauerhafter, pfadfreier Exportplan. PlanId ist der SHA-256 ueber alle
/// fachlichen Entscheidungen; GeneratedUtc ist reine Audit-Information.
/// </summary>
public sealed record TrainingExportPlan(
    string SchemaVersion,
    string PlanId,
    DateTimeOffset GeneratedUtc,
    string InventoryRunId,
    IReadOnlyDictionary<string, string> SourceSnapshotHashes,
    int ClassMapVersion,
    string VsaManifestHash,
    string RegistryHash,
    IReadOnlyList<TrainingExportProtectedSetReference> ProtectedSets,
    IReadOnlyList<string> Classes,
    IReadOnlyList<string> TrainHoldingKeys,
    IReadOnlyList<string> ValidationHoldingKeys,
    IReadOnlyDictionary<string, int> InstancesPerClass,
    IReadOnlyList<TrainingExportPlannedImage> Images,
    IReadOnlyList<TrainingExportExclusion> Exclusions)
{
    public const string CurrentSchemaVersion = "2.0";
}

/// <summary>
/// Laufzeit-Huelle fuer die Ausfuehrer. Nur sie enthaelt lokale Originalpfade;
/// der serialisierte Plan bleibt dadurch portabel und enthaelt keine Kundendatenpfade.
/// </summary>
public sealed record TrainingExportPlanBundle(
    TrainingExportPlan Plan,
    IReadOnlyDictionary<string, string> SourcePathsByImageSha256);
