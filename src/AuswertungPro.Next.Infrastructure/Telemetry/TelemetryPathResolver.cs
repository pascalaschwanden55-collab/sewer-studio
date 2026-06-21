namespace AuswertungPro.Next.Infrastructure.Telemetry;

public static class TelemetryPathResolver
{
    public const string TelemetryDirEnvVar = "SEWERSTUDIO_TELEMETRY_DIR";

    public static string? ResolveFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var overrideDir = Environment.GetEnvironmentVariable(TelemetryDirEnvVar);
        var root = !string.IsNullOrWhiteSpace(overrideDir)
            ? overrideDir
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return string.IsNullOrWhiteSpace(root)
            ? null
            : Path.Combine(root, "SewerStudio", "Telemetry", fileName);
    }
}
