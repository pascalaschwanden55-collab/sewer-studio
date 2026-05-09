using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Polls CPU, RAM, and GPU utilization every 2 seconds.
/// Uses LibreHardwareMonitor (CPU/RAM/GPU sensors), P/Invoke for CPU%+RAM, nvidia-smi for NVIDIA GPU details.
/// Falls back to WMI (MSAcpi_ThermalZoneTemperature) for CPU temp when LHM is unavailable.
/// All properties notify via INotifyPropertyChanged on the UI dispatcher.
/// </summary>
public sealed partial class SystemMonitorService : INotifyPropertyChanged, IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Dispatcher _dispatcher;

    // CPU delta tracking
    private long _prevIdleTicks;
    private long _prevTotalTicks;

    // GPU (nvidia-smi fast path)
    private string? _nvidiaSmiPath;
    private bool _gpuAvailable = true;
    private int _gpuQuerySkip;
    private int _gpuFailCount;

    // LibreHardwareMonitor
    private volatile Computer? _computer;
    private volatile bool _hwInitDone;
    private volatile bool _hwRetried;
    private int _hwMonitorSkip;

    // WMI CPU temp fallback (via process)
    private bool _wmiTempAvailable = true;
    private int _wmiTempSkip;
    private int _wmiTempFailCount;

    // HWiNFO Shared Memory fallback (works with HVCI because HWiNFO uses WHQL-signed driver)
    private bool _hwInfoAvailable = true;
    private int _hwInfoSkip;
    private bool _hwInfoLogged;

    // Tatsaechliche CPU-Frequenz via PerfCounter "\Processor Information(_Total)\% Processor Performance"
    // PROCESSOR_POWER_INFORMATION.CurrentMhz liefert auf modernen Intel-CPUs (Core Ultra 9 etc.) oft nur
    // die Base-Clock und nicht den aktuellen Boost-Takt. Performance-Counter × MaxMhz ist genauer.
    private int _cpuMaxMhz;
    private System.Diagnostics.PerformanceCounter? _cpuPerfCounter;
    private bool _cpuPerfCounterTried;
    private bool _cpuPerfCounterAvailable;

    // HVCI detection
    private bool _hvciChecked;
    private bool _isHvciEnabled;

    // Diagnostic log
    private readonly List<string> _diagLog = new();
    private string _diagnosticSummary = "";
    public string DiagnosticSummary { get => _diagnosticSummary; private set => Set(ref _diagnosticSummary, value); }

    /// <summary>True when HVCI (Memory Integrity) blocks hardware sensor drivers.</summary>
    private bool _isSensorBlocked;
    public bool IsSensorBlocked { get => _isSensorBlocked; private set => Set(ref _isSensorBlocked, value); }

    private string _sensorBlockedReason = "";
    public string SensorBlockedReason { get => _sensorBlockedReason; private set => Set(ref _sensorBlockedReason, value); }

    public SystemMonitorService()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _timer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _timer.Tick += OnTick;

        // Seed CPU counters
        GetSystemTimes(out var idle, out var kernel, out var user);
        _prevIdleTicks = FileTimeToLong(idle);
        _prevTotalTicks = FileTimeToLong(kernel) + FileTimeToLong(user);

        // CPU name from registry
        ReadCpuName();

        // Find nvidia-smi
        _nvidiaSmiPath = FindNvidiaSmi();
        if (_nvidiaSmiPath is null)
        {
            _gpuAvailable = false;
            Log("nvidia-smi: NICHT gefunden");
        }
        else
        {
            Log($"nvidia-smi: {_nvidiaSmiPath}");
        }

        // Init LibreHardwareMonitor (async to not block UI)
        Task.Run(InitHardwareMonitor);

        // Einmalige RAM-Geschwindigkeit via WMI - klappt ohne Treiber & ohne HVCI-Konflikt.
        // Wird vom HWiNFO-Pfad ueberschrieben falls live-Wert verfuegbar.
        Task.Run(InitRamClockFromWmi);
    }

    /// <summary>
    /// Liest die konfigurierte RAM-Geschwindigkeit aus Win32_PhysicalMemory.
    /// Liefert die hoechste gefundene ConfiguredClockSpeed (oder Speed als Fallback).
    /// Funktioniert ohne Admin und ohne Hardware-Sensoren.
    /// </summary>
    private void InitRamClockFromWmi()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NoLogo -Command \""
                    + "$m = Get-CimInstance Win32_PhysicalMemory -ErrorAction SilentlyContinue; "
                    + "if ($m) { ($m | ForEach-Object { if ($_.ConfiguredClockSpeed) { $_.ConfiguredClockSpeed } else { $_.Speed } } | Measure-Object -Maximum).Maximum } "
                    + "else { '0' }\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is null) return;
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(5000);

            if (int.TryParse(output, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mhz)
                && mhz > 0 && mhz < 20000)
            {
                _dispatcher.BeginInvoke(() =>
                {
                    if (!IsRamClockAvailable)
                    {
                        RamClockMhz = mhz;
                        IsRamClockAvailable = true;
                    }
                });
                Log($"RAM Clock via WMI: {mhz} MHz (Fallback ohne Treiber).");
            }
        }
        catch (Exception ex)
        {
            Log($"RAM Clock via WMI fehlgeschlagen: {ex.Message}");
        }
    }

    // ── Properties ───────────────────────────────────────────────────────

    private int _cpuPercent;
    public int CpuPercent { get => _cpuPercent; private set => Set(ref _cpuPercent, value); }

    private int _cpuClockMhz;
    public int CpuClockMhz { get => _cpuClockMhz; private set => Set(ref _cpuClockMhz, value); }

    private bool _isCpuClockAvailable;
    public bool IsCpuClockAvailable { get => _isCpuClockAvailable; private set => Set(ref _isCpuClockAvailable, value); }

    private int _cpuTempC;
    public int CpuTempC { get => _cpuTempC; private set => Set(ref _cpuTempC, value); }

    private bool _isCpuTempAvailable;
    public bool IsCpuTempAvailable { get => _isCpuTempAvailable; private set => Set(ref _isCpuTempAvailable, value); }

    private string _cpuName = "";
    public string CpuName { get => _cpuName; private set => Set(ref _cpuName, value); }

    private long _ramUsedMb;
    public long RamUsedMb { get => _ramUsedMb; private set => Set(ref _ramUsedMb, value); }

    private long _ramTotalMb;
    public long RamTotalMb { get => _ramTotalMb; private set => Set(ref _ramTotalMb, value); }

    private int _ramPercent;
    public int RamPercent { get => _ramPercent; private set => Set(ref _ramPercent, value); }

    private int _ramClockMhz;
    public int RamClockMhz { get => _ramClockMhz; private set => Set(ref _ramClockMhz, value); }

    private int _ramTempC;
    public int RamTempC { get => _ramTempC; private set => Set(ref _ramTempC, value); }

    private bool _isRamTempAvailable;
    public bool IsRamTempAvailable { get => _isRamTempAvailable; private set => Set(ref _isRamTempAvailable, value); }

    private bool _isRamClockAvailable;
    public bool IsRamClockAvailable { get => _isRamClockAvailable; private set => Set(ref _isRamClockAvailable, value); }

    private int _gpuPercent;
    public int GpuPercent { get => _gpuPercent; private set => Set(ref _gpuPercent, value); }

    private long _gpuMemUsedMb;
    public long GpuMemUsedMb { get => _gpuMemUsedMb; private set => Set(ref _gpuMemUsedMb, value); }

    private long _gpuMemTotalMb;
    public long GpuMemTotalMb { get => _gpuMemTotalMb; private set => Set(ref _gpuMemTotalMb, value); }

    private int _gpuMemPercent;
    public int GpuMemPercent { get => _gpuMemPercent; private set => Set(ref _gpuMemPercent, value); }

    private int _gpuTempC;
    public int GpuTempC { get => _gpuTempC; private set => Set(ref _gpuTempC, value); }

    private int _gpuClockMhz;
    public int GpuClockMhz { get => _gpuClockMhz; private set => Set(ref _gpuClockMhz, value); }

    private bool _isGpuTempAvailable;
    public bool IsGpuTempAvailable { get => _isGpuTempAvailable; private set => Set(ref _isGpuTempAvailable, value); }

    private bool _isGpuClockAvailable;
    public bool IsGpuClockAvailable { get => _isGpuClockAvailable; private set => Set(ref _isGpuClockAvailable, value); }

    private string _gpuName = "";
    public string GpuName { get => _gpuName; private set => Set(ref _gpuName, value); }

    private bool _isGpuAvailable;
    public bool IsGpuAvailable { get => _isGpuAvailable; private set => Set(ref _isGpuAvailable, value); }

    // ── Start / Stop ─────────────────────────────────────────────────────

    public void Start()
    {
        Poll(); // immediate first reading
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    // ── Tick ──────────────────────────────────────────────────────────────

    private void OnTick(object? sender, EventArgs e) => Poll();

    private void Poll()
    {
        PollCpu();
        PollCpuClock();
        PollRam();
        PollGpu();
        PollHardwareMonitor();
        PollHwInfo();
        PollCpuTempFallback();
    }

    // ── Diagnostic logging ───────────────────────────────────────────────

    private void Log(string message)
    {
        var line = $"[Monitor] {message}";
        Trace.WriteLine(line);
        lock (_diagLog)
        {
            _diagLog.Add(line);
            if (_diagLog.Count > 50)
                _diagLog.RemoveAt(0);
        }

        // Update summary on UI thread
        try
        {
            _dispatcher.BeginInvoke(() =>
            {
                string summary;
                lock (_diagLog)
                    summary = string.Join("\n", _diagLog);
                DiagnosticSummary = summary;
            });
        }
        catch { /* dispatcher might be shut down */ }
    }

    // ── LibreHardwareMonitor Init ────────────────────────────────────────

    private void InitHardwareMonitor()
    {
        try
        {
            // Check HVCI once
            if (!_hvciChecked)
            {
                _hvciChecked = true;
                _isHvciEnabled = DetectHvci();
                if (_isHvciEnabled)
                    Log("HVCI: Memory Integrity ist AKTIV — Hardware-Sensortreiber blockiert");
            }

            Log("LHM: Initialisierung gestartet...");

            var computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsStorageEnabled = false,
                IsNetworkEnabled = false,
                IsControllerEnabled = false,
                IsBatteryEnabled = false,
                IsPsuEnabled = false
            };
            computer.Open();
            Log("LHM: computer.Open() erfolgreich");

            // Update + enumerate all hardware to discover sensors
            foreach (var root in computer.Hardware)
                UpdateHardwareTree(root);

            // Log all discovered hardware and sensors for diagnostics
            int totalSensors = 0;
            foreach (var hw in EnumerateHardwareTree(computer))
            {
                var sensorCount = hw.Sensors.Length;
                totalSensors += sensorCount;

                if (IsMonitoredHardwareType(hw.HardwareType))
                {
                    Log($"LHM: {hw.HardwareType} '{hw.Name}' — {sensorCount} Sensoren");

                    foreach (var s in hw.Sensors)
                    {
                        if (s.SensorType == SensorType.Temperature || s.SensorType == SensorType.Clock)
                        {
                            Log($"  -> {s.SensorType}: {s.Name} = {s.Value?.ToString("F1") ?? "null"}");
                        }
                    }
                }
            }

            bool hasAnySensors = EnumerateHardwareTree(computer)
                .Any(hw => IsMonitoredHardwareType(hw.HardwareType) && hw.Sensors.Length > 0);

            if (hasAnySensors)
            {
                _computer = computer;
                Log($"LHM: OK — {totalSensors} Sensoren aktiv");
            }
            else
            {
                computer.Close();
                _computer = null;

                var reason = _isHvciEnabled
                    ? "HVCI (Kernisolierung) blockiert Sensor-Treiber.\nOption 1: HWiNFO64 starten (Shared Memory aktivieren) — funktioniert mit HVCI.\nOption 2: Kernisolierung deaktivieren unter Windows-Sicherheit > Geraetesicherheit."
                    : "Keine Sensoren gefunden (Admin-Rechte? Treiber?)";
                Log($"LHM: FEHLGESCHLAGEN — {(reason.Replace('\n', ' '))}");

                _dispatcher.BeginInvoke(() =>
                {
                    IsSensorBlocked = true;
                    SensorBlockedReason = reason;
                });
            }
        }
        catch (Exception ex)
        {
            _computer = null;
            Log($"LHM: EXCEPTION — {ex.GetType().Name}: {ex.Message}");

            var reason = _isHvciEnabled
                ? "HVCI blockiert Sensor-Treiber"
                : $"Sensor-Fehler: {ex.Message}";
            _dispatcher.BeginInvoke(() =>
            {
                IsSensorBlocked = true;
                SensorBlockedReason = reason;
            });
        }
        finally
        {
            _hwInitDone = true;
        }
    }

    private static bool DetectHvci()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity");
            if (key?.GetValue("Enabled") is int enabled)
                return enabled == 1;
        }
        catch (Exception ex)
        {
            // Phase 1.2: Empty-catch-Sweep — Debug-Log statt stilles Schlucken.
            // Registry-Lese-Fehler sind nicht-kritisch (HVCI-Erkennung), aber bei
            // Rechte-/Manifest-Aenderungen relevant fuer Diagnose.
            System.Diagnostics.Debug.WriteLine($"[SystemMonitor] DetectHvci: {ex.GetType().Name}: {ex.Message}");
        }
        return false;
    }

    // ── CPU clock via CallNtPowerInformation (kein Admin noetig) ─────────

    private void PollCpuClock()
    {
        try
        {
            int processorCount = Environment.ProcessorCount;
            int structSize = Marshal.SizeOf<PROCESSOR_POWER_INFORMATION>();
            int bufferSize = processorCount * structSize;
            var buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                uint status = CallNtPowerInformation(
                    ProcessorInformation, IntPtr.Zero, 0, buffer, (uint)bufferSize);
                if (status != 0) return;

                long sumClock = 0;
                long sumMax = 0;
                int validCount = 0;
                for (int i = 0; i < processorCount; i++)
                {
                    var info = Marshal.PtrToStructure<PROCESSOR_POWER_INFORMATION>(
                        buffer + i * structSize);
                    var mhz = (int)info.CurrentMhz;
                    var maxMhz = (int)info.MaxMhz;
                    if (mhz <= 0)
                        continue;
                    sumClock += mhz;
                    sumMax += maxMhz;
                    validCount++;
                }

                if (validCount > 0)
                {
                    int avgCurrent = (int)Math.Round((double)sumClock / validCount);
                    int avgMax = (int)Math.Round((double)sumMax / validCount);
                    if (avgMax > 0) _cpuMaxMhz = avgMax;

                    // Performance-Counter-basierte Frequenz (genauer auf modernen Intel CPUs).
                    int? perfBasedMhz = TryGetPerfCounterMhz(_cpuMaxMhz);
                    CpuClockMhz = perfBasedMhz ?? avgCurrent;
                    IsCpuClockAvailable = true;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch { /* keep last known value */ }
    }

    /// <summary>
    /// Berechnet aktuelle CPU-Frequenz aus PerformanceCounter "% Processor Performance" * MaxMhz.
    /// Liefert null wenn Counter nicht verfuegbar (z.B. auf Win Home / fehlende Performance-Bibliothek).
    /// </summary>
    private int? TryGetPerfCounterMhz(int maxMhz)
    {
        if (maxMhz <= 0) return null;
        if (!_cpuPerfCounterTried)
        {
            _cpuPerfCounterTried = true;
            try
            {
                _cpuPerfCounter = new System.Diagnostics.PerformanceCounter(
                    "Processor Information", "% Processor Performance", "_Total", readOnly: true);
                _cpuPerfCounter.NextValue(); // erste Lesung verwerfen (immer 0)
                _cpuPerfCounterAvailable = true;
                Log("PerfCounter '% Processor Performance' aktiv (genauerer Takt).");
            }
            catch (Exception ex)
            {
                _cpuPerfCounterAvailable = false;
                Log($"PerfCounter '% Processor Performance' nicht verfuegbar ({ex.GetType().Name}); fallback auf NtPowerInfo.");
            }
        }
        if (!_cpuPerfCounterAvailable || _cpuPerfCounter is null)
            return null;

        try
        {
            var pct = _cpuPerfCounter.NextValue();
            // pct ist in Prozent zur Base-Clock - Werte > 100 = Boost.
            // Bei "MaxMhz" handelt es sich um die Boost-fähige Max-Frequenz, daher
            // ist der "Echte" Takt = MaxMhz * pct / 100, capped auf MaxMhz wenn pct < 100.
            // Korrektur: % Processor Performance ist relativ zur "nominal frequency" = Base-Clock.
            // Da NtPower's MaxMhz die Boost-Freq ist und der Counter relativ zur Base ist,
            // brauchen wir hier eine konservative Schaetzung. Wir nehmen pct als reine Skala:
            //   Takt = MaxMhz * pct / 100 (kann > MaxMhz werden bei Boost - das ist OK)
            var mhz = (int)Math.Round(maxMhz * pct / 100.0);
            return mhz > 0 ? mhz : null;
        }
        catch
        {
            return null;
        }
    }

    // ── CPU/RAM/GPU sensors via LibreHardwareMonitor (alle ~4s) ──────────

    // ── CPU temp fallback via WMI/PerfCounter (wenn LHM fehlschlaegt) ───

    // Stage 1: PerfCounter (kein Admin noetig, Win10+)
    private bool _perfCounterTempAvailable = true;
    private int _perfCounterTempFailCount;


    private static IEnumerable<IHardware> EnumerateHardwareTree(Computer computer)
    {
        foreach (var hardware in computer.Hardware)
        {
            foreach (var item in EnumerateHardwareTree(hardware))
                yield return item;
        }
    }

    private static IEnumerable<IHardware> EnumerateHardwareTree(IHardware hardware)
    {
        yield return hardware;
        foreach (var sub in hardware.SubHardware)
        {
            foreach (var item in EnumerateHardwareTree(sub))
                yield return item;
        }
    }

    private static void UpdateHardwareTree(IHardware hardware)
    {
        hardware.Update();
        foreach (var sub in hardware.SubHardware)
            UpdateHardwareTree(sub);
    }

    private static bool IsMonitoredHardwareType(HardwareType hardwareType)
        => hardwareType == HardwareType.Cpu
           || hardwareType == HardwareType.Memory
           || hardwareType == HardwareType.Motherboard
           || hardwareType == HardwareType.SuperIO
           || IsGpuHardwareType(hardwareType);

    // ── CPU via GetSystemTimes ────────────────────────────────────────────

    private void PollCpu()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
            return;

        var idleTicks = FileTimeToLong(idle);
        var totalTicks = FileTimeToLong(kernel) + FileTimeToLong(user);

        var deltaIdle = idleTicks - _prevIdleTicks;
        var deltaTotal = totalTicks - _prevTotalTicks;

        _prevIdleTicks = idleTicks;
        _prevTotalTicks = totalTicks;

        if (deltaTotal <= 0)
            return;

        CpuPercent = (int)Math.Round(100.0 * (deltaTotal - deltaIdle) / deltaTotal);
    }

    // ── RAM via GlobalMemoryStatusEx ──────────────────────────────────────

    private void PollRam()
    {
        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref mem))
            return;

        var totalMb = (long)(mem.ullTotalPhys / (1024UL * 1024));
        var availMb = (long)(mem.ullAvailPhys / (1024UL * 1024));
        var usedMb = totalMb - availMb;

        RamTotalMb = totalMb;
        RamUsedMb = usedMb;
        RamPercent = totalMb > 0 ? (int)Math.Round(100.0 * usedMb / totalMb) : 0;
    }

    // ── CPU name from registry ───────────────────────────────────────────

    private void ReadCpuName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            if (key is null) return;

            if (key.GetValue("ProcessorNameString") is string name)
                CpuName = name.Trim();

            // Fallback clock until LibreHardwareMonitor delivers
            if (TryReadInt(key.GetValue("~MHz"), out var mhz) && mhz > 0)
            {
                CpuClockMhz = mhz;
                IsCpuClockAvailable = true;
            }
        }
        catch { /* registry not accessible */ }
    }

    private static bool TryReadInt(object? value, out int result)
    {
        switch (value)
        {
            case int i:
                result = i;
                return true;
            case uint ui when ui <= int.MaxValue:
                result = (int)ui;
                return true;
            case long l when l is >= int.MinValue and <= int.MaxValue:
                result = (int)l;
                return true;
            case string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                result = parsed;
                return true;
            default:
                result = 0;
                return false;
        }
    }

    // ── GPU via nvidia-smi ────────────────────────────────────────────────


    // ── P/Invoke ──────────────────────────────────────────────────────────

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(
        out FILETIME lpIdleTime,
        out FILETIME lpKernelTime,
        out FILETIME lpUserTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("PowrProf.dll")]
    private static extern uint CallNtPowerInformation(
        int InformationLevel, IntPtr InputBuffer, uint InputBufferLength,
        IntPtr OutputBuffer, uint OutputBufferLength);

    private const int ProcessorInformation = 11;

    private static long FileTimeToLong(FILETIME ft)
        => ((long)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public int dwLowDateTime;
        public int dwHighDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESSOR_POWER_INFORMATION
    {
        public uint Number;
        public uint MaxMhz;
        public uint CurrentMhz;
        public uint MhzLimit;
        public uint MaxIdleState;
        public uint CurrentIdleState;
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public void Dispose()
    {
        _timer.Stop();
        try { _computer?.Close(); } catch { }
        try { _cpuPerfCounter?.Dispose(); } catch { }
    }
}
