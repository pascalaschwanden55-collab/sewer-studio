using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using AuswertungPro.Next.Application.Common;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.QgisBridge;

/// <summary>
/// Zugangstoken der QGIS-Bridge (Gesamtaudit 2026-08-14, P1-3).
///
/// Vorher war die Bridge zwar auf Loopback begrenzt, lief aber standardmaessig ohne
/// jede Anmeldung: Jedes andere Programm auf demselben Rechner konnte Projekt- und
/// Geodaten abrufen. "Nur lokal" ist keine Grenze, wenn mehrere Programme lokal laufen.
///
/// Aufbau wie beim Live-Control-Token: Env-Var hat Vorrang, sonst wird beim Start ein
/// Zufallstoken erzeugt und in eine Datei gelegt, die das QGIS-Plugin desselben
/// Benutzers lesen kann. Der Token ist damit nie leer — es gibt keinen anmeldefreien Weg.
/// </summary>
internal static class QgisBridgeToken
{
    public const string HeaderName = "X-QGIS-Bridge-Token";
    public const string EnvVarName = "SEWERSTUDIO_QGIS_BRIDGE_TOKEN";
    public const string FileName = ".qgis_bridge_token";

    /// <summary>
    /// Ablage der Tokendatei — gleiche Ableitung wie <c>AppSettings.AppDataDir</c>,
    /// damit das Plugin exakt denselben Pfad berechnen kann.
    /// </summary>
    public static string TokenFilePath
        => Path.Combine(AppDataPathResolver.Resolve(AppIdentity.ProductName), FileName);

    /// <summary>
    /// Liefert den Token: bevorzugt aus der Env-Var, sonst neu erzeugt und abgelegt.
    /// Gibt nie null oder leer zurueck.
    /// </summary>
    public static string ResolveOrCreate(ILogger logger)
    {
        var envToken = Environment.GetEnvironmentVariable(EnvVarName);
        if (!string.IsNullOrWhiteSpace(envToken))
            return envToken;

        var generated = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        try
        {
            var path = TokenFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, generated);
            logger.LogInformation("QGIS-Bridge-Token erzeugt und abgelegt: {Path}", path);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "QGIS-Bridge-Token konnte nicht gespeichert werden — die Anmeldung bleibt aktiv. "
                + "Das QGIS-Plugin braucht dann den Token aus {EnvVar}.",
                EnvVarName);
        }

        return generated;
    }

    /// <summary>
    /// Zeitkonstanter Vergleich (gegen Timing-Angriffe). Ein leerer oder fehlender
    /// Token des Aufrufers passt nie, weil der erwartete Token nie leer ist.
    /// </summary>
    public static bool Matches(string? expected, string? provided)
    {
        if (string.IsNullOrEmpty(expected))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(provided ?? string.Empty));
    }
}
