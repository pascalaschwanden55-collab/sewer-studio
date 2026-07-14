using System.Threading;
using AuswertungPro.Next.Application.Ai.Startup;

namespace AuswertungPro.Next.Infrastructure.Ai.Startup;

/// <summary>Kompatibilitätsfassade für die Sidecar-Startpfadsuche.</summary>
public static class SidecarScriptLocator
{
    private static ISidecarScriptLocator _current = new SidecarScriptFileLocator();

    public static ISidecarScriptLocator Current => Volatile.Read(ref _current);

    public static void Use(ISidecarScriptLocator locator)
        => Volatile.Write(
            ref _current,
            locator ?? throw new ArgumentNullException(nameof(locator)));

    public static string? FindDefaultSidecarScript()
        => Current.FindDefaultSidecarScript();

    public static string ResolvePowerShellExe()
        => Current.ResolvePowerShellExe();
}
