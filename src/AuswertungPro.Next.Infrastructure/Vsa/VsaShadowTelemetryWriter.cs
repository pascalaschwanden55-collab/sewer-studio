using AuswertungPro.Next.Application.Vsa;

namespace AuswertungPro.Next.Infrastructure.Vsa;

/// <summary>Unveraenderliche Kompatibilitaetsfassade; neue Aufrufer verwenden den Instanzdienst.</summary>
public static class VsaShadowTelemetryWriter
{
    private static readonly IVsaShadowTelemetryWriter Default = new VsaShadowTelemetryFileWriter();

    public static IVsaShadowTelemetryWriter Current => Default;

    [Obsolete("Globaler Austausch wurde entfernt. Den Schreiber per Konstruktor uebergeben.")]
    public static void Use(IVsaShadowTelemetryWriter writer) =>
        throw new NotSupportedException(
            "Der globale VSA-Schreiber kann nicht mehr ausgetauscht werden. " +
            "IVsaShadowTelemetryWriter bitte per Konstruktor uebergeben.");

    public static void Write(VsaShadowTelemetryEvent entry, string? pathOverride = null)
        => Default.Write(entry, pathOverride);

    public static string? ResolvePath() => Default.ResolvePath();
}
