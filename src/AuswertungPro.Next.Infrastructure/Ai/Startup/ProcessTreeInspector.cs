using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AuswertungPro.Next.Infrastructure.Ai.Startup;

/// <summary>
/// Liest die Vorfahrenkette eines Prozesses ueber NtQueryInformationProcess
/// (InheritedFromUniqueProcessId). Dient der Besitzpruefung beim kontrollierten
/// Sidecar-Neustart (Paket 3/A2): Nur ein Sidecar, zu dessen Vorfahren ein selbst
/// gestarteter Prozess gehoert, darf beendet und neu gestartet werden.
/// Paket 2/A3: Zusaetzlich Identitaets-Probe (Startzeit/Programmdatei) und ein
/// erfolgsmeldendes KillProcessTree — ein Kill, der fehlschlaegt oder in ein Timeout
/// laeuft, darf nie zu einem zweiten Sidecar-Start fuehren.
/// </summary>
public static class ProcessTreeInspector
{
    private const int ProcessBasicInformationClass = 0;
    private const int ProcessQueryLimitedInformation = 0x1000;
    private const int MaxChainDepth = 16;

    /// <summary>
    /// Liefert die Vorfahren-PIDs (naechster Vorfahre zuerst). Leer bei Fehler oder
    /// wenn die Kette nicht lesbar ist — die Besitzpruefung faellt dann sicher auf
    /// "fremd" (kein Neustart) statt zu raten.
    /// </summary>
    public static IReadOnlyList<int> GetAncestorIds(int processId)
    {
        if (!OperatingSystem.IsWindows() || processId <= 0)
            return Array.Empty<int>();

        var ancestors = new List<int>();
        var visited = new HashSet<int> { processId };
        var current = processId;

        for (var depth = 0; depth < MaxChainDepth; depth++)
        {
            var parent = GetParentId(current);
            if (parent is null or <= 0 || !visited.Add(parent.Value))
                break;
            ancestors.Add(parent.Value);
            current = parent.Value;
        }

        return ancestors;
    }

    private static int? GetParentId(int processId)
    {
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (handle == IntPtr.Zero)
            return null;

        try
        {
            var info = new PROCESS_BASIC_INFORMATION();
            var status = NtQueryInformationProcess(
                handle,
                ProcessBasicInformationClass,
                ref info,
                Marshal.SizeOf<PROCESS_BASIC_INFORMATION>(),
                out _);
            if (status != 0)
                return null;
            var parentId = info.InheritedFromUniqueProcessId.ToInt64();
            return parentId is > 0 and <= int.MaxValue ? (int)parentId : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_BASIC_INFORMATION
    {
        public int ExitStatus;
        public IntPtr PebBaseAddress;
        public IntPtr AffinityMask;
        public int BasePriority;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        ref PROCESS_BASIC_INFORMATION processInformation,
        int processInformationLength,
        out int returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    /// <summary>
    /// Liest die Identitaet eines laufenden Prozesses defensiv (Paket 2/A3):
    /// Found=false, wenn der Prozess nicht (mehr) existiert; StartTimeUtc/ImagePath sind
    /// null, wenn der Wert nicht lesbar ist (Zugriffsfehler) — der Aufrufer entscheidet
    /// dann konservativ (kein Kill ohne bewiesene Identitaet).
    /// </summary>
    public static ProcessIdentityProbe ProbeProcessIdentity(int processId)
    {
        if (processId <= 0)
            return new ProcessIdentityProbe(false, null, null);

        try
        {
            using var process = Process.GetProcessById(processId);
            if (process.HasExited)
                return new ProcessIdentityProbe(false, null, null);

            DateTime? startTimeUtc = null;
            try
            {
                startTimeUtc = process.StartTime.ToUniversalTime();
            }
            catch
            {
                // Zugriffsfehler: keine Aussage — die Identitaet ist dann nicht bewiesen.
            }

            string? imagePath = null;
            try
            {
                imagePath = process.MainModule?.FileName;
            }
            catch
            {
                // Zugriffsfehler (z. B. geschuetzter Prozess): keine Aussage.
            }

            return new ProcessIdentityProbe(true, startTimeUtc, imagePath);
        }
        catch
        {
            return new ProcessIdentityProbe(false, null, null);
        }
    }

    /// <summary>
    /// Beendet einen Prozess samt Baum und wartet auf das tatsaechliche Ende (Paket 2/A3).
    /// true = Kill aufgerufen und Prozess innerhalb des Timeouts beendet (oder er lief
    /// bereits nicht mehr); false bei Fehler oder Timeout — der Aufrufer darf dann KEINEN
    /// Ersatzprozess starten (Doppelstart-/Portkonflikt-Risiko).
    /// </summary>
    public static bool KillProcessTree(int processId, TimeSpan waitTimeout)
    {
        if (processId <= 0)
            return false;

        try
        {
            using var process = Process.GetProcessById(processId);
            if (process.HasExited)
                return true;

            process.Kill(entireProcessTree: true);
            return process.WaitForExit((int)Math.Max(0, waitTimeout.TotalMilliseconds));
        }
        catch (ArgumentException)
        {
            // Prozess existiert nicht (mehr) — das Kill-Ziel ist erreicht.
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Vergleicht zwei Programmdatei-Angaben ueber den Dateinamen (Paket 2/A3). Der
    /// Vergleich toleriert eine fehlende Extension ("powershell" == "powershell.exe"),
    /// weil Startauftraege den Namen je nach Aufloesung mit oder ohne Pfad/Extension
    /// tragen. Leere Angaben liefern keine Aussage (true) — die Startzeit bleibt der
    /// primaere Identitaetsanker.
    /// </summary>
    internal static bool ImageFileNameMatches(string actualPath, string expectedPath)
    {
        var actualName = Path.GetFileName(actualPath.Trim().Trim('"'));
        var expectedName = Path.GetFileName(expectedPath.Trim().Trim('"'));
        if (actualName.Length == 0 || expectedName.Length == 0)
            return true;

        if (string.Equals(actualName, expectedName, StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(
            Path.GetFileNameWithoutExtension(actualName),
            Path.GetFileNameWithoutExtension(expectedName),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True, wenn die Programmdatei ein Python-Interpreter ist (python.exe, python3.exe,
    /// pythonw.exe; venv- oder System-Installation). Bindet den Python-Kindprozess des
    /// Sidecars vor einem Kill an die erwartete Programmdatei (Paket 2/B3); null oder
    /// unlesbar = false (kein Kill ohne bewiesene Identitaet).
    /// </summary>
    internal static bool IsPythonInterpreterImage(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return false;
        var name = Path.GetFileNameWithoutExtension(imagePath.Trim().Trim('"'));
        return name.StartsWith("python", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Best-effort-Identitaet eines laufenden Prozesses (Paket 2/A3).</summary>
/// <param name="Found">false = Prozess existiert nicht (mehr).</param>
/// <param name="StartTimeUtc">Startzeit (UTC); null = nicht lesbar.</param>
/// <param name="ImagePath">Programmdatei; null = nicht lesbar.</param>
public readonly record struct ProcessIdentityProbe(bool Found, DateTime? StartTimeUtc, string? ImagePath);
