using AuswertungPro.Next.Application.Ai.Training.ClassMaps;
using AuswertungPro.Next.Application.Ai.Training.Inventory;

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
    DateTimeOffset GeneratedUtc);

public sealed record TrainingExportPlannedLabel(
    int ClassId,
    string ClassName,
    TrainingExportBoundingBox BoundingBox,
    IReadOnlyList<TrainingExportSourceRef> Sources);

public sealed record TrainingExportPlannedImage(
    string ImageSha256,
    string HoldingKey,
    TrainingExportTarget Target,
    string TargetFileName,
    IReadOnlyList<TrainingExportPlannedLabel> Labels);

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
