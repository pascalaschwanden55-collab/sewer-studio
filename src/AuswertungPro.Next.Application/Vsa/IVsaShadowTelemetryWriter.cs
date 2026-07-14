namespace AuswertungPro.Next.Application.Vsa;

/// <summary>Schreibt Abweichungen zwischen alter und neuer VSA-Klassifizierung.</summary>
public interface IVsaShadowTelemetryWriter
{
    void Write(VsaShadowTelemetryEntry entry, string? pathOverride = null);

    string? ResolvePath();
}

public record VsaShadowTelemetryEntry(
    DateTimeOffset TimestampUtc,
    string Code,
    string BaseCode,
    string Requirement,
    int? LegacyEz,
    int? V2Ez,
    bool ExpectedDrift,
    string? V2Reason = null,
    string? Ch1 = null,
    string? Ch2 = null,
    string? Q1 = null,
    string? Q2 = null,
    string? Material = null,
    string? Dn = null,
    string? V2RuleId = null,
    string? V2SourceRef = null);
