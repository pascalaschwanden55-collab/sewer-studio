using AuswertungPro.Next.Application.Diagnostics;

namespace AuswertungPro.Next.Infrastructure.Telemetry;

public sealed class TelemetryFilePathResolver : ITelemetryPathResolver
{
    private readonly Func<string, string?> _getEnvironmentVariable;
    private readonly Func<Environment.SpecialFolder, string> _getFolderPath;

    public TelemetryFilePathResolver()
        : this(Environment.GetEnvironmentVariable, Environment.GetFolderPath)
    {
    }

    public TelemetryFilePathResolver(
        Func<string, string?> getEnvironmentVariable,
        Func<Environment.SpecialFolder, string> getFolderPath)
    {
        _getEnvironmentVariable = getEnvironmentVariable
            ?? throw new ArgumentNullException(nameof(getEnvironmentVariable));
        _getFolderPath = getFolderPath
            ?? throw new ArgumentNullException(nameof(getFolderPath));
    }

    public string? ResolveFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var overrideDir = _getEnvironmentVariable(TelemetryPathResolver.TelemetryDirEnvVar);
        var root = !string.IsNullOrWhiteSpace(overrideDir)
            ? overrideDir
            : _getFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return string.IsNullOrWhiteSpace(root)
            ? null
            : Path.Combine(root, "SewerStudio", "Telemetry", fileName);
    }
}

/// <summary>Kompatibilitaetsfassade fuer bestehende Aufrufer.</summary>
public static class TelemetryPathResolver
{
    public const string TelemetryDirEnvVar = "SEWERSTUDIO_TELEMETRY_DIR";

    private static readonly ITelemetryPathResolver Default = new TelemetryFilePathResolver();

    public static ITelemetryPathResolver Current => Default;

    [Obsolete("Globaler Austausch wurde entfernt. Den Dienst per Konstruktor uebergeben.")]
    public static void Use(ITelemetryPathResolver resolver) =>
        throw new NotSupportedException(
            "Die globale Telemetrie-Pfadsuche kann nicht mehr ausgetauscht werden. " +
            "ITelemetryPathResolver bitte per Konstruktor uebergeben.");

    public static string? ResolveFile(string fileName) => Current.ResolveFile(fileName);
}
