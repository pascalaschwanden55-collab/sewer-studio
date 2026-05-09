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

    private void PollHardwareMonitor()
    {
        if (_computer is null)
        {
            // If init finished but failed, retry once after ~30 seconds
            if (_hwInitDone && !_hwRetried && _hwMonitorSkip++ > 15)
            {
                _hwRetried = true;
                Log("LHM: Retry-Versuch...");
                Task.Run(InitHardwareMonitor);
            }
            return;
        }

        if (_hwMonitorSkip++ % 2 != 0)
            return;

        try
        {
            int cpuTempC = 0;
            int cpuClockMhz = 0;
            bool cpuTempFound = false;
            bool cpuClockFound = false;
            int boardCpuTempC = 0;
            bool boardCpuTempFound = false;

            // RAM sensors
            int ramClockMhz = 0;
            int ramTempC = 0;
            bool ramTempFound = false;
            bool ramClockFound = false;
            int boardRamTempC = 0;
            bool boardRamTempFound = false;

            // GPU sensors (fallback when nvidia-smi is unavailable or incomplete)
            int gpuLoadPercent = 0;
            int gpuClockMhz = 0;
            int gpuTempC = 0;
            bool gpuLoadFound = false;
            bool gpuClockFound = false;
            bool gpuTempFound = false;
            string? gpuName = null;

            // Update all roots + sub-hardware first, then read sensors.
            foreach (var root in _computer.Hardware)
                UpdateHardwareTree(root);

            foreach (var hw in EnumerateHardwareTree(_computer))
            {
                if (hw.HardwareType == HardwareType.Cpu)
                {
                    foreach (var sensor in hw.Sensors)
                    {
                        if (!sensor.Value.HasValue)
                            continue;

                        if (sensor.SensorType == SensorType.Temperature)
                        {
                            var sensorName = sensor.Name ?? string.Empty;
                            var temp = (int)Math.Round(sensor.Value.Value);

                            // Prefer package temp, otherwise use the highest reasonable reading.
                            if (!cpuTempFound
                                || sensorName.Contains("Package", StringComparison.OrdinalIgnoreCase)
                                || temp > cpuTempC)
                            {
                                cpuTempC = temp;
                                cpuTempFound = true;
                            }
                        }

                        if (sensor.SensorType == SensorType.Clock)
                        {
                            var sensorName = sensor.Name ?? string.Empty;
                            if (sensorName.Contains("Core", StringComparison.OrdinalIgnoreCase)
                                || sensorName.Contains("CPU", StringComparison.OrdinalIgnoreCase))
                            {
                                var clock = (int)Math.Round(sensor.Value.Value);
                                if (!cpuClockFound || clock > cpuClockMhz)
                                {
                                    cpuClockMhz = clock;
                                    cpuClockFound = true;
                                }
                            }
                        }
                    }
                }
                else if (hw.HardwareType == HardwareType.Memory)
                {
                    foreach (var sensor in hw.Sensors)
                    {
                        if (!sensor.Value.HasValue)
                            continue;

                        if (sensor.SensorType == SensorType.Clock
                            && (int)sensor.Value.Value > ramClockMhz)
                        {
                            ramClockMhz = (int)Math.Round(sensor.Value.Value);
                            ramClockFound = true;
                        }

                        if (sensor.SensorType == SensorType.Temperature)
                        {
                            ramTempC = (int)Math.Round(sensor.Value.Value);
                            ramTempFound = true;
                        }
                    }
                }
                else if (IsGpuHardwareType(hw.HardwareType))
                {
                    if (string.IsNullOrWhiteSpace(gpuName) && !string.IsNullOrWhiteSpace(hw.Name))
                        gpuName = hw.Name.Trim();

                    foreach (var sensor in hw.Sensors)
                    {
                        if (!sensor.Value.HasValue)
                            continue;

                        if (sensor.SensorType == SensorType.Load)
                        {
                            var sensorName = sensor.Name ?? string.Empty;
                            var load = (int)Math.Round(sensor.Value.Value);
                            if (!gpuLoadFound
                                || sensorName.Contains("Core", StringComparison.OrdinalIgnoreCase)
                                || sensorName.Contains("GPU", StringComparison.OrdinalIgnoreCase)
                                || sensorName.Contains("3D", StringComparison.OrdinalIgnoreCase)
                                || load > gpuLoadPercent)
                            {
                                gpuLoadPercent = load;
                                gpuLoadFound = true;
                            }
                        }

                        if (sensor.SensorType == SensorType.Clock)
                        {
                            var sensorName = sensor.Name ?? string.Empty;
                            var clock = (int)Math.Round(sensor.Value.Value);
                            if (!gpuClockFound
                                || sensorName.Contains("Core", StringComparison.OrdinalIgnoreCase)
                                || sensorName.Contains("Graphics", StringComparison.OrdinalIgnoreCase)
                                || sensorName.Contains("GPU", StringComparison.OrdinalIgnoreCase)
                                || clock > gpuClockMhz)
                            {
                                gpuClockMhz = clock;
                                gpuClockFound = true;
                            }
                        }

                        if (sensor.SensorType == SensorType.Temperature)
                        {
                            var temp = (int)Math.Round(sensor.Value.Value);
                            if (!gpuTempFound || temp > gpuTempC)
                            {
                                gpuTempC = temp;
                                gpuTempFound = true;
                            }
                        }
                    }
                }
                else if (hw.HardwareType == HardwareType.Motherboard
                         || hw.HardwareType == HardwareType.SuperIO)
                {
                    foreach (var sensor in hw.Sensors)
                    {
                        if (!sensor.Value.HasValue || sensor.SensorType != SensorType.Temperature)
                            continue;

                        var name = sensor.Name ?? string.Empty;
                        var temp = (int)Math.Round(sensor.Value.Value);
                        if (temp <= 0 || temp >= 150)
                            continue;

                        if (name.Contains("CPU", StringComparison.OrdinalIgnoreCase)
                            || name.Contains("Package", StringComparison.OrdinalIgnoreCase)
                            || name.Contains("Tctl", StringComparison.OrdinalIgnoreCase)
                            || name.Contains("Die", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!boardCpuTempFound || temp > boardCpuTempC)
                            {
                                boardCpuTempC = temp;
                                boardCpuTempFound = true;
                            }
                        }

                        if (name.Contains("RAM", StringComparison.OrdinalIgnoreCase)
                            || name.Contains("DRAM", StringComparison.OrdinalIgnoreCase)
                            || name.Contains("DIMM", StringComparison.OrdinalIgnoreCase)
                            || name.Contains("Memory", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!boardRamTempFound || temp > boardRamTempC)
                            {
                                boardRamTempC = temp;
                                boardRamTempFound = true;
                            }
                        }
                    }
                }
            }

            // Mainboard-Floor-Fallback (boardCpuTempC) NICHT mehr als CPU-Temp uebernehmen -
            // das war oft 28°C konstant und hat HWiNFO suppressiert (siehe BUGFIX in PollHwInfo).
            // CPU-Temp braucht echte Package/Tctl/Core/Die-Sensoren, sonst lieber n/a.
            if (cpuTempFound && cpuTempC > 0 && cpuTempC < 150)
            {
                CpuTempC = cpuTempC;
                IsCpuTempAvailable = true;
            }

            // Prefer live sensor clock whenever available.
            if (cpuClockFound && cpuClockMhz > 0)
            {
                CpuClockMhz = cpuClockMhz;
                IsCpuClockAvailable = true;
            }

            if (ramClockFound && ramClockMhz > 0)
            {
                RamClockMhz = ramClockMhz;
                IsRamClockAvailable = true;
            }

            if (!ramTempFound && boardRamTempFound)
            {
                ramTempC = boardRamTempC;
                ramTempFound = true;
            }

            if (ramTempFound && ramTempC > 0 && ramTempC < 120)
            {
                RamTempC = ramTempC;
                IsRamTempAvailable = true;
            }

            if (!string.IsNullOrWhiteSpace(gpuName))
                GpuName = gpuName;

            if (gpuLoadFound && gpuLoadPercent >= 0 && gpuLoadPercent <= 100)
            {
                GpuPercent = Math.Clamp(gpuLoadPercent, 0, 100);
                IsGpuAvailable = true;
            }

            if (gpuClockFound && gpuClockMhz > 0)
            {
                GpuClockMhz = gpuClockMhz;
                IsGpuClockAvailable = true;
                IsGpuAvailable = true;
            }

            if (gpuTempFound && gpuTempC > 0 && gpuTempC < 150)
            {
                GpuTempC = gpuTempC;
                IsGpuTempAvailable = true;
                IsGpuAvailable = true;
            }
        }
        catch (Exception ex)
        {
            Log($"LHM Poll: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // ── HWiNFO Shared Memory fallback (HVCI-kompatibel) ─────────────────

    private const string HwInfoSensorsSm2 = "Global\\HWiNFO_SENS_SM2";

    // HWiNFO Shared Memory reading types
    private const int SENSOR_TYPE_NONE = 0;
    private const int SENSOR_TYPE_TEMP = 1;
    private const int SENSOR_TYPE_VOLT = 2;
    private const int SENSOR_TYPE_FAN = 3;
    private const int SENSOR_TYPE_CURRENT = 4;
    private const int SENSOR_TYPE_POWER = 5;
    private const int SENSOR_TYPE_CLOCK = 6;
    private const int SENSOR_TYPE_USAGE = 7;
    private const int SENSOR_TYPE_OTHER = 8;

    private void PollHwInfo()
    {
        // BUGFIX 2026-05-03: vorher wurde HWiNFO komplett uebersprungen sobald LHM
        // *irgendeinen* Temp-Wert lieferte (z.B. Mainboard-Floor 28°C bei Intel Core
        // Ultra 9 als CPU-Temp interpretiert). Folge: HWiNFO-RAM-Temp + GPU-Temp +
        // realer CPU-Tdie nie gelesen. Gate komplett entfernt - HWiNFO laeuft jetzt
        // immer und ueberschreibt unzuverlaessige LHM-Werte mit echten DTS-Sensoren.
        if (!_hwInfoAvailable)
            return;

        // Don't run until LHM init is done
        if (!_hwInitDone)
            return;

        // Poll every ~4 seconds
        if (_hwInfoSkip++ % 2 != 0)
            return;

        try
        {
            using var mmf = MemoryMappedFile.OpenExisting(HwInfoSensorsSm2, MemoryMappedFileRights.Read);
            using var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            // Read header (first 44 bytes)
            // Offsets: 0=dwSignature(4), 4=dwVersion(4), 8=dwRevision(4), 12=pollTime(8),
            // 20=dwOffsetOfSensorSection(4), 24=dwSizeOfSensorElement(4),
            // 28=dwNumSensorElements(4), 32=dwOffsetOfReadingSection(4),
            // 36=dwSizeOfReadingElement(4), 40=dwNumReadingElements(4)
            uint signature = accessor.ReadUInt32(0);
            // HWiNFO 4-char-Signaturen ueber verschiedene Versionen:
            //   0x53695748 = bytes [H,W,i,S] = "HWiS" (klassisch v2)
            //   0x53694857 = bytes [W,H,i,S] = HWiNFO neuer FourCC (manche Builds)
            //   0x53696857 = bytes [W,h,i,S] = "WhiS" (alternative SDK-Quellen)
            // Wir akzeptieren alle drei. Wenn andere Signatur: nur warnen, nicht
            // permanent disablen - vielleicht hat HWiNFO gerade neu initialisiert.
            bool sigOk = signature == 0x53695748
                      || signature == 0x53694857
                      || signature == 0x53696857;
            if (!sigOk)
            {
                if (!_hwInfoLogged)
                {
                    Log($"HWiNFO: Signatur 0x{signature:X8} unbekannt (erwartet HWiS/WHiS/WhiS) - retry naechster Cycle");
                    _hwInfoLogged = true;
                }
                return; // nicht _hwInfoAvailable=false, sondern naechster Tick versuchen
            }

            uint offsetReadings = accessor.ReadUInt32(32);
            uint sizeReading = accessor.ReadUInt32(36);
            uint numReadings = accessor.ReadUInt32(40);

            if (numReadings == 0 || sizeReading < 100)
            {
                if (!_hwInfoLogged) { Log("HWiNFO: Keine Sensordaten in Shared Memory"); _hwInfoLogged = true; }
                return;
            }

            if (!_hwInfoLogged)
            {
                Log($"HWiNFO: Shared Memory aktiv — {numReadings} Messwerte");
                _hwInfoLogged = true;
                // Reset von alten Fehlerzustaenden, falls HWiNFO neu gestartet wurde.
                _hwInfoAvailable = true;
                IsSensorBlocked = false;
                SensorBlockedReason = "";
            }

            bool cpuTempSet = false;
            bool cpuClockSet = false;
            bool gpuTempSet = false;
            bool gpuClockSet = false;
            bool ramTempSet = false;
            bool ramClockSet = false;

            var labelBytes = new byte[128];

            for (uint i = 0; i < numReadings; i++)
            {
                long pos = offsetReadings + (long)i * sizeReading;

                // Reading struct: tReading(4), dwSensorIndex(4), dwReadingID(4),
                // szLabelOrig(128), szLabelUser(128), szUnit(16), dValue(8), ...
                int readingType = accessor.ReadInt32(pos);
                // Skip non-temp, non-clock
                if (readingType != SENSOR_TYPE_TEMP && readingType != SENSOR_TYPE_CLOCK)
                    continue;

                // Read label (128 bytes at offset 12)
                accessor.ReadArray(pos + 12, labelBytes, 0, 128);
                var label = Encoding.ASCII.GetString(labelBytes).TrimEnd('\0').ToLowerInvariant();

                // Value is at offset 12 + 128 (labelOrig) + 128 (labelUser) + 16 (unit) = offset 284
                double value = accessor.ReadDouble(pos + 284);

                if (readingType == SENSOR_TYPE_TEMP)
                {
                    int tempC = (int)Math.Round(value);
                    if (tempC <= 0 || tempC >= 150) continue;

                    if (!cpuTempSet && (label.Contains("cpu") || label.Contains("package")
                                        || label.Contains("core") || label.Contains("tctl")
                                        || label.Contains("die")))
                    {
                        CpuTempC = tempC;
                        IsCpuTempAvailable = true;
                        cpuTempSet = true;
                    }
                    else if (!gpuTempSet && (label.Contains("gpu") || label.Contains("graphics")))
                    {
                        GpuTempC = tempC;
                        IsGpuTempAvailable = true;
                        gpuTempSet = true;
                    }
                    else if (!ramTempSet && (label.Contains("dimm") || label.Contains("memory")
                                              || label.Contains("ram") || label.Contains("ddr")))
                    {
                        RamTempC = tempC;
                        IsRamTempAvailable = true;
                        ramTempSet = true;
                    }
                }
                else if (readingType == SENSOR_TYPE_CLOCK)
                {
                    int clockMhz = (int)Math.Round(value);
                    if (clockMhz <= 0) continue;

                    if (!cpuClockSet && (label.Contains("cpu") || label.Contains("core")))
                    {
                        CpuClockMhz = clockMhz;
                        IsCpuClockAvailable = true;
                        cpuClockSet = true;
                    }
                    else if (!gpuClockSet && (label.Contains("gpu") || label.Contains("graphics")))
                    {
                        GpuClockMhz = clockMhz;
                        IsGpuClockAvailable = true;
                        gpuClockSet = true;
                    }
                    else if (!ramClockSet && (label.Contains("memory clock") || label.Contains("dram")
                                               || label.Contains("ram clock") || label.Contains("ddr")
                                               || (label.Contains("memory") && label.Contains("clock"))))
                    {
                        RamClockMhz = clockMhz;
                        IsRamClockAvailable = true;
                        ramClockSet = true;
                    }
                }
            }

            // Clear sensor-blocked state since HWiNFO is providing data
            if (cpuTempSet || gpuTempSet)
            {
                IsSensorBlocked = false;
                SensorBlockedReason = "";
            }
        }
        catch (FileNotFoundException)
        {
            // HWiNFO not running or Shared Memory not enabled — retry next cycle
            if (!_hwInfoLogged)
            {
                Log("HWiNFO: Shared Memory nicht gefunden (HWiNFO laeuft nicht oder SM nicht aktiviert)");
                _hwInfoLogged = true;

                if (IsSensorBlocked)
                {
                    SensorBlockedReason += "\nTipp: HWiNFO64 starten mit Shared Memory Support fuer Temp-Anzeige trotz HVCI.";
                }
            }
        }
        catch (Exception ex)
        {
            // BUGFIX: Vorher hat ein einmaliger Fehler HWiNFO PERMANENT disabled
            // (_hwInfoAvailable=false). Wenn z.B. HWiNFO gerade neu startet oder die
            // Shared Memory kurzzeitig nicht lesbar ist, ging die Anzeige fuer den
            // ganzen App-Lauf verloren. Jetzt: nur loggen, _hwInfoAvailable bleibt true,
            // naechster Poll-Tick versucht es erneut.
            if (!_hwInfoLogged)
            {
                Log($"HWiNFO: Fehler — {ex.GetType().Name}: {ex.Message} (retry naechster Cycle)");
                _hwInfoLogged = true;
            }
        }
    }

    // ── CPU temp fallback via WMI/PerfCounter (wenn LHM fehlschlaegt) ───

    // Stage 1: PerfCounter (kein Admin noetig, Win10+)
    private bool _perfCounterTempAvailable = true;
    private int _perfCounterTempFailCount;

    private void PollCpuTempFallback()
    {
        // Only use fallback when LHM is not providing CPU temp
        if (IsCpuTempAvailable)
            return;

        // Don't run until LHM init is complete (give LHM a chance first)
        if (!_hwInitDone)
            return;

        // Only query every ~10 seconds (every 5th tick)
        if (_wmiTempSkip++ % 5 != 0)
            return;

        // Stage 1: Performance Counter WMI class (kein Admin noetig)
        if (_perfCounterTempAvailable)
        {
            Task.Run(PollCpuTempPerfCounter);
            return;
        }

        // Stage 2: ACPI Thermal Zone (braucht Admin)
        if (_wmiTempAvailable)
        {
            Task.Run(PollCpuTempAcpi);
        }
    }

    /// <summary>
    /// Liest CPU-Temperatur ueber Win32_PerfFormattedData_Counters_ThermalZoneInformation.
    /// Funktioniert OHNE Admin-Rechte auf Windows 10/11.
    /// Gibt Temperatur in Kelvin zurueck (z.B. 323 = 50 °C).
    /// </summary>
    private void PollCpuTempPerfCounter()
    {
        try
        {
            // Liest ALLE ThermalZones, gibt MAX und MIN zurueck. Konstant-niedrige Mainboard-
            // Floor-Werte (~28-30 °C, nicht reagierend auf CPU-Last) werden als nicht plausibel
            // gefiltert. Nur wenn die Spreizung >= 5 °C ueber Min-Floor liegt, gilt MAX als CPU-Temp.
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NoLogo -Command \""
                    + "$zones = Get-CimInstance Win32_PerfFormattedData_Counters_ThermalZoneInformation -ErrorAction SilentlyContinue | "
                    + "Where-Object { $_.Temperature -gt 200 -and $_.Temperature -lt 420 } | "
                    + "ForEach-Object { [math]::Round($_.Temperature - 273.15) }; "
                    + "if ($zones) { $max = ($zones | Measure-Object -Maximum).Maximum; $min = ($zones | Measure-Object -Minimum).Minimum; \"$max;$min\" } else { '0;0' }\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc is null) return;

            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(5000);

            var parts = output.Split(';');
            int maxC = 0, minC = 0;
            if (parts.Length >= 2)
            {
                int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out maxC);
                int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out minC);
            }

            if (maxC > 0 && maxC < 150)
            {
                // Mainboard-Floor-Filter: konstant niedrige Werte mit < 5 °C Spreizung sind
                // Mainboard/Chipset-Sensoren, nicht die CPU. Markiere als "nicht verfuegbar".
                bool plausibleCpuTemp = maxC >= 35 || (maxC - minC) >= 5;
                if (plausibleCpuTemp)
                {
                    _perfCounterTempFailCount = 0;
                    _dispatcher.BeginInvoke(() =>
                    {
                        CpuTempC = maxC;
                        IsCpuTempAvailable = true;
                    });

                    if (_wmiTempSkip <= 6)
                        Log($"PerfCounter CPU-Temp: {maxC} °C (Min={minC} °C, alle Zonen) — kein Admin noetig");
                    return;
                }
                else
                {
                    if (_wmiTempSkip <= 6)
                        Log($"PerfCounter CPU-Temp: nur Mainboard-Floor erkannt ({maxC} °C, Spreizung {maxC - minC} °C). Wahrscheinlich keine CPU-DTS-Zone.");
                }
            }

            if (++_perfCounterTempFailCount >= 3)
            {
                _perfCounterTempAvailable = false;
                Log("PerfCounter CPU-Temp: nicht verfuegbar, versuche ACPI Fallback...");
            }
        }
        catch
        {
            if (++_perfCounterTempFailCount >= 3)
            {
                _perfCounterTempAvailable = false;
                Log("PerfCounter CPU-Temp: fehlgeschlagen, versuche ACPI Fallback...");
            }
        }
    }

    /// <summary>
    /// Liest CPU-Temperatur ueber MSAcpi_ThermalZoneTemperature (braucht Admin).
    /// </summary>
    private void PollCpuTempAcpi()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NoLogo -Command \"$t = Get-CimInstance -Namespace root/WMI -ClassName MSAcpi_ThermalZoneTemperature -ErrorAction SilentlyContinue | Select-Object -First 1; if($t){$t.CurrentTemperature}else{'0'}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc is null) return;

            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(5000);

            if (int.TryParse(output, NumberStyles.Integer, CultureInfo.InvariantCulture, out var raw) && raw > 0)
            {
                // MSAcpi_ThermalZoneTemperature returns temp in tenths of Kelvin
                var celsius = (int)Math.Round((raw / 10.0) - 273.15);
                if (celsius > 0 && celsius < 150)
                {
                    _wmiTempFailCount = 0;
                    _dispatcher.BeginInvoke(() =>
                    {
                        CpuTempC = celsius;
                        IsCpuTempAvailable = true;
                    });

                    if (_wmiTempSkip <= 6)
                        Log($"ACPI CPU-Temp Fallback: {celsius} °C (Admin-Modus)");
                    return;
                }
            }

            if (++_wmiTempFailCount >= 3)
            {
                _wmiTempAvailable = false;
                Log("ACPI CPU-Temp: nicht verfuegbar auf diesem System");
            }
        }
        catch
        {
            if (++_wmiTempFailCount >= 3)
            {
                _wmiTempAvailable = false;
                Log("ACPI CPU-Temp: PowerShell-Abfrage fehlgeschlagen");
            }
        }
    }

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
