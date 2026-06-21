using System;
using System.IO;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Loest das gemeinsame Vision-Sidecar-Token fuer C#-Clients auf.
/// Kanonischer Env-Name ist SEWERSTUDIO_SIDECAR_TOKEN; Legacy-Aliase bleiben gueltig.
/// </summary>
public static class SidecarTokenResolver
{
    public const string HeaderName = "X-Sidecar-Token";

    private static readonly string[] EnvironmentVariableNames =
    [
        "SEWERSTUDIO_SIDECAR_TOKEN",
        "AUSWERTUNGPRO_SIDECAR_TOKEN",
        "SEWER_SIDECAR_AUTH_TOKEN",
        "SEWER_SIDECAR_TOKEN"
    ];

    public static string? Resolve(string? configuredToken = null)
    {
        var explicitToken = Normalize(configuredToken);
        if (explicitToken is not null)
            return explicitToken;

        foreach (var name in EnvironmentVariableNames)
        {
            var token = Normalize(Environment.GetEnvironmentVariable(name));
            if (token is not null)
                return token;
        }

        return TryReadTokenFile();
    }

    public static string? Normalize(string? token)
        => string.IsNullOrWhiteSpace(token) ? null : token.Trim();

    private static string? TryReadTokenFile()
    {
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
                return null;

            var path = Path.Combine(localAppData, "SewerStudio", ".sidecar_token");
            return File.Exists(path)
                ? Normalize(File.ReadAllText(path))
                : null;
        }
        catch
        {
            return null;
        }
    }
}
