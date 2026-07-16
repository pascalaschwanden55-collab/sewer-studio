using AuswertungPro.Next.Application.Ai.Startup;

namespace AuswertungPro.Next.Infrastructure.Ai.Startup;

/// <summary>Kompatibilitätsfassade für die Sidecar-Startpfadsuche.</summary>
public static class SidecarScriptLocator
{
    private static readonly ISidecarScriptLocator Default = new SidecarScriptFileLocator();

    public static ISidecarScriptLocator Current => Default;

    [Obsolete("Globaler Austausch wurde entfernt. Den Dienst per Konstruktor uebergeben.")]
    public static void Use(ISidecarScriptLocator locator)
        => throw new NotSupportedException(
            "Die globale Sidecar-Startpfadsuche kann nicht mehr ausgetauscht werden. " +
            "ISidecarScriptLocator bitte per Konstruktor uebergeben.");

    public static string? FindDefaultSidecarScript()
        => Current.FindDefaultSidecarScript();

    public static string ResolvePowerShellExe()
        => Current.ResolvePowerShellExe();
}
