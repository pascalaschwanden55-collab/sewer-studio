using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Löst das Sidecar-Token aus Einstellung, Umgebungsvariablen oder der lokalen Token-Datei auf.
/// </summary>
public sealed class SidecarTokenFileResolver : ISidecarTokenResolver
{
    private static readonly string[] EnvironmentVariableNames =
    [
        "SEWERSTUDIO_SIDECAR_TOKEN",
        "AUSWERTUNGPRO_SIDECAR_TOKEN",
        "SEWER_SIDECAR_AUTH_TOKEN",
        "SEWER_SIDECAR_TOKEN"
    ];

    private readonly Func<string, string?> _environmentVariableReader;
    private readonly Func<string?> _tokenFilePathProvider;

    public SidecarTokenFileResolver()
    {
        _environmentVariableReader = Environment.GetEnvironmentVariable;
        _tokenFilePathProvider = GetDefaultTokenFilePath;
    }

    public SidecarTokenFileResolver(
        Func<string, string?> environmentVariableReader,
        string? tokenFilePath)
    {
        _environmentVariableReader = environmentVariableReader
            ?? throw new ArgumentNullException(nameof(environmentVariableReader));
        _tokenFilePathProvider = () => tokenFilePath;
    }

    public string? Resolve(string? configuredToken = null)
    {
        var explicitToken = Normalize(configuredToken);
        if (explicitToken is not null)
            return explicitToken;

        foreach (var name in EnvironmentVariableNames)
        {
            var token = Normalize(_environmentVariableReader(name));
            if (token is not null)
                return token;
        }

        return TryReadTokenFile();
    }

    internal static string? Normalize(string? token)
        => string.IsNullOrWhiteSpace(token) ? null : token.Trim();

    private string? TryReadTokenFile()
    {
        try
        {
            var path = _tokenFilePathProvider();
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path)
                ? Normalize(File.ReadAllText(path))
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetDefaultTokenFilePath()
    {
        var localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrWhiteSpace(localAppData)
            ? null
            : Path.Combine(localAppData, "SewerStudio", ".sidecar_token");
    }
}
