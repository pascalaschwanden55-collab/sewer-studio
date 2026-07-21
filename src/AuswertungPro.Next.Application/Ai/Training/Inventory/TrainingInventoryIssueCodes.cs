namespace AuswertungPro.Next.Application.Ai.Training.Inventory;

/// <summary>Stabile Codes fuer maschinenlesbare Probleme im Inventarbericht.</summary>
public static class TrainingInventoryIssueCodes
{
    public const string SearchRootMissing = "search-root-missing";
    public const string ProtectedRootMissing = "protected-root-missing";
    public const string DirectorySkipped = "directory-skipped";
    public const string SourceDiscoveryFailed = "source-discovery-failed";
    public const string SourceInvalid = "source-invalid";
    public const string SourceMissing = "source-missing";
    public const string EvalProtectionUnavailable = "eval-protection-unavailable";
    public const string EvalHashCheckDisabled = "eval-hash-check-disabled";
    public const string PathInvalid = "path-invalid";
    public const string AssetHashReadError = "asset-hash-read-error";
}
