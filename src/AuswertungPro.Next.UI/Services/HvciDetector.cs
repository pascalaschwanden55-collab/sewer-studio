namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Reine Entscheidungslogik für HVCI (Memory Integrity / Hypervisor-Enforced Code Integrity).
/// Der eigentliche Registry-Zugriff verbleibt im Service; dieser Helfer wertet nur den gelesenen Wert aus.
/// </summary>
public static class HvciDetector
{
    /// <summary>
    /// Gibt true zurück, wenn der aus der Registry gelesene Wert HVCI als aktiv kennzeichnet.
    /// </summary>
    /// <param name="registryValue">
    /// Der Wert aus HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity\Enabled,
    /// oder null wenn der Schlüssel nicht gelesen werden konnte.
    /// </param>
    /// <returns>true wenn HVCI aktiv (Enabled == 1), sonst false.</returns>
    public static bool IsEnabled(object? registryValue)
        => registryValue is int enabled && enabled == 1;
}
