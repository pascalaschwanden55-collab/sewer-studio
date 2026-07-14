namespace AuswertungPro.Next.Application.Ai.Startup;

/// <summary>Sucht das Sidecar-Startskript und die passende Windows-PowerShell.</summary>
public interface ISidecarScriptLocator
{
    string? FindDefaultSidecarScript();

    string ResolvePowerShellExe();
}
