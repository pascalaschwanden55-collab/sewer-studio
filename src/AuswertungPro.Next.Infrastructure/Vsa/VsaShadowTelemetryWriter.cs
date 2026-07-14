using AuswertungPro.Next.Application.Vsa;

namespace AuswertungPro.Next.Infrastructure.Vsa;

/// <summary>Kompatibilitaetsfassade; die Dateiarbeit liegt im Instanzdienst.</summary>
public static class VsaShadowTelemetryWriter
{
    private static IVsaShadowTelemetryWriter _current = new VsaShadowTelemetryFileWriter();

    public static IVsaShadowTelemetryWriter Current => Volatile.Read(ref _current);

    public static void Use(IVsaShadowTelemetryWriter writer) =>
        Volatile.Write(ref _current, writer ?? throw new ArgumentNullException(nameof(writer)));

    public static void Write(VsaShadowTelemetryEvent entry, string? pathOverride = null)
        => Current.Write(entry, pathOverride);

    public static string? ResolvePath() => Current.ResolvePath();
}

public sealed record VsaShadowTelemetryEvent(
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
    string? V2SourceRef = null)
    : VsaShadowTelemetryEntry(
        TimestampUtc,
        Code,
        BaseCode,
        Requirement,
        LegacyEz,
        V2Ez,
        ExpectedDrift,
        V2Reason,
        Ch1,
        Ch2,
        Q1,
        Q2,
        Material,
        Dn,
        V2RuleId,
        V2SourceRef);
