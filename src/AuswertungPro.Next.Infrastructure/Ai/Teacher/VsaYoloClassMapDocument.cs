namespace AuswertungPro.Next.Infrastructure.Ai.Teacher;

internal enum VsaYoloClassMapFormat
{
    Legacy,
    Versioned
}

internal sealed record VsaYoloClassMapDocument(
    VsaYoloClassMapFormat Format,
    Dictionary<string, int> Classes,
    int? Version = null,
    string? VsaManifestHash = null)
{
    public VsaYoloClassMapDocument WithClasses(Dictionary<string, int> classes)
        => this with { Classes = classes };
}
