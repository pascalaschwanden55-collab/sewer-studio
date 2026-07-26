using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

/// <summary>
/// Fuehrt Junction-Tests nur aus, wenn die aktuelle Windows-Testumgebung echte
/// Verzeichnis-Verknuepfungen anlegen kann. Ein fehlendes Recht wird bei der
/// Testentdeckung sichtbar als Skip gemeldet.
/// </summary>
internal sealed class JunctionFactAttribute : FactAttribute
{
    public JunctionFactAttribute()
    {
        Skip = JunctionTestSupport.UnavailableReason;
    }
}

internal static class JunctionTestSupport
{
    private static readonly Lazy<string?> ProbeResult = new(Probe);

    public static string? UnavailableReason => ProbeResult.Value;

    public static void CreateDirectoryLink(string link, string target)
        => Directory.CreateSymbolicLink(link, target);

    private static string? Probe()
    {
        if (!OperatingSystem.IsWindows())
            return "Junction-Sicherheitstest benoetigt Windows.";

        var root = Path.Combine(
            Path.GetTempPath(),
            "sewerstudio-junction-probe-" + Guid.NewGuid().ToString("N"));
        var target = Path.Combine(root, "target");
        var link = Path.Combine(root, "link");
        try
        {
            Directory.CreateDirectory(target);
            Directory.CreateSymbolicLink(link, target);
            return null;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException
                                   or IOException
                                   or PlatformNotSupportedException)
        {
            return $"Verzeichnis-Verknuepfungen sind nicht verfuegbar: {ex.Message}";
        }
        finally
        {
            try
            {
                if (Directory.Exists(link))
                    Directory.Delete(link);
            }
            catch
            {
                // Nur Testprobe-Aufraeumen.
            }

            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch
            {
                // Nur Testprobe-Aufraeumen.
            }
        }
    }
}
