using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Common;
using Microsoft.Win32;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Polls CPU, RAM, and GPU utilization every 2 seconds.
/// Uses LibreHardwareMonitor (CPU/RAM/GPU sensors), P/Invoke for CPU%+RAM, nvidia-smi for NVIDIA GPU details.
/// Falls back to WMI (MSAcpi_ThermalZoneTemperature) for CPU temp when LHM is unavailable.
/// All properties notify via INotifyPropertyChanged on the UI dispatcher.
/// </summary>
public sealed class SystemMonitorService : INotifyPropertyChanged, IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Dispatcher _dispatcher;
    private readonly bool _enableHardwareSensorInit;
    private int _disposed;

    // CPU delta tracking
    private long _prevIdleTicks;
    private long _prevTotalTicks;

    // GPU (nvidia-smi fast path)
    private string? _nvidiaSmiPath;
    // volatile/Interlocked: aus Poll-Task geschrieben, vom UI-Timer-Thread gelesen.
    private volatile bool _gpuAvailable = true;
    private int _gpuQuerySkip;
    private int _gpuFailCount;

    // LibreHardwareMonitor
    private readonly LibreHardwareMonitorSensor _libreHardwareSensor;

    // WMI CPU temp fallback (via process)
    private volatile bool _wmiTempAvailable = true;
    private int _wmiTempSkip;
    private int _wmiTempFailCount;

    // HWiNFO Shared Memory fallback (works with HVCI because HWiNFO uses WHQL-signed driver)
    private bool _hwInfoAvailable = true;
    private int _hwInfoSkip;
    private bool _hwInfoLogged;
    private volatile bool _hwInfoProvidesTemp; // true wenn HWiNFO aktuell eine CPU-Temp liefert (Live-Quelle)

    // Diagnostic log
    private readonly List<string> _diagLog = new();
    private string _diagnosticSummary = "";
    public string DiagnosticSummary { get => _diagnosticSummary; private set => Set(ref _diagnosticSummary, value); }

    /// <summary>True when HVCI (Memory Integrity) blocks hardware sensor drivers.</summary>
    private bool _isSensorBlocked;
    public bool IsSensorBlocked { get => _isSensorBlocked; private set => Set(ref _isSensorBlocked, value); }

    private string _sensorBlockedReason = "";
    public string SensorBlockedReason { get => _sensorBlockedReason; private set => Set(ref _sensorBlockedReason, value); }

    public SystemMonitorService(bool enableHardwareSensorInit = true)
    {
        _enableHardwareSensorInit = enableHardwareSensorInit;
        _libreHardwareSensor = new LibreHardwareMonitorSensor(enableHardwareSensorInit);
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

        // Init LibreHardwareMonitor (async to not block UI). Tests can disable this because
        // some native hardware telemetry drivers crash the process instead of throwing.
        if (_enableHardwareSensorInit)
            Task.Run(InitHardwareMonitor);
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

    private string _cpuTempStatusText = "Temperatur wird gesucht";
    public string CpuTempStatusText { get => _cpuTempStatusText; private set => Set(ref _cpuTempStatusText, value); }

    private string _cpuTempSourceLabel = "Quelle: ausstehend";
    public string CpuTempSourceLabel { get => _cpuTempSourceLabel; private set => Set(ref _cpuTempSourceLabel, value); }

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
        if (Volatile.Read(ref _disposed) != 0)
            return;

        Poll(); // immediate first reading
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    // ── Tick ──────────────────────────────────────────────────────────────

    private void OnTick(object? sender, EventArgs e) => Poll();

    private void Poll()
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        PollCpu();
        PollCpuClock();
        PollRam();
        PollGpu();
        PollHardwareMonitor();
        PollHwInfo();
        PollCpuTempFallback();
    }

    private void SetCpuTempReading(int tempC, string source)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        CpuTempC = tempC;
        IsCpuTempAvailable = true;
        CpuTempSourceLabel = $"Quelle: {source}";
        CpuTempStatusText = source == "Windows Thermal Zone"
            ? "Fallback-Wert von Windows. Je nach PC ist das nicht immer die echte CPU-Package-Temperatur."
            : "CPU-Temperatur aktiv";
    }

    private void SetCpuTempUnavailable(string reason)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        if (IsCpuTempAvailable)
            return;

        CpuTempSourceLabel = "Quelle: nicht verfügbar";
        CpuTempStatusText = reason;
    }

    // ── Diagnostic logging ───────────────────────────────────────────────

    private void Log(string message)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

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
                if (Volatile.Read(ref _disposed) != 0)
                    return;

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
        if (!_enableHardwareSensorInit)
            return;

        if (Volatile.Read(ref _disposed) != 0)
            return;

        var result = _libreHardwareSensor.Initialize();
        foreach (var message in result.Messages)
            Log(message);

        if (result.Succeeded || string.IsNullOrWhiteSpace(result.FailureReason))
            return;

        _dispatcher.BeginInvoke(() =>
        {
            IsSensorBlocked = true;
            SensorBlockedReason = result.FailureReason;
            SetCpuTempUnavailable(result.TemperatureUnavailableReason
                                  ?? "CPU-Temperatur nicht verfügbar: Sensorzugriff fehlgeschlagen.");
        });
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
                int validCount = 0;
                for (int i = 0; i < processorCount; i++)
                {
                    var info = Marshal.PtrToStructure<PROCESSOR_POWER_INFORMATION>(
                        buffer + i * structSize);
                    var mhz = (int)info.CurrentMhz;
                    if (mhz <= 0)
                        continue;
                    sumClock += mhz;
                    validCount++;
                }

                if (validCount > 0)
                {
                    CpuClockMhz = (int)Math.Round((double)sumClock / validCount);
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

    // ── CPU/RAM/GPU sensors via LibreHardwareMonitor (alle ~4s) ──────────

    private void PollHardwareMonitor()
    {
        var result = _libreHardwareSensor.Poll();
        if (result.RetryRequested)
        {
            Log("LHM: Retry-Versuch...");
            Task.Run(InitHardwareMonitor);
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            Log(result.Error);
            return;
        }

        var reading = result.Reading;
        if (reading is null)
            return;

        if (reading.CpuTempC is int cpuTempC && cpuTempC > 0 && cpuTempC < 150)
            SetCpuTempReading(cpuTempC, "LibreHardwareMonitor");

        if (reading.CpuClockMhz is int cpuClockMhz && cpuClockMhz > 0)
        {
            CpuClockMhz = cpuClockMhz;
            IsCpuClockAvailable = true;
        }

        if (reading.RamClockMhz is int ramClockMhz && ramClockMhz > 0)
        {
            RamClockMhz = ramClockMhz;
            IsRamClockAvailable = true;
        }

        if (reading.RamTempC is int ramTempC && ramTempC > 0 && ramTempC < 120)
        {
            RamTempC = ramTempC;
            IsRamTempAvailable = true;
        }

        if (!string.IsNullOrWhiteSpace(reading.GpuName))
            GpuName = reading.GpuName;

        if (reading.GpuLoadPercent is int gpuLoadPercent && gpuLoadPercent is >= 0 and <= 100)
        {
            GpuPercent = Math.Clamp(gpuLoadPercent, 0, 100);
            IsGpuAvailable = true;
        }

        if (reading.GpuClockMhz is int gpuClockMhz && gpuClockMhz > 0)
        {
            GpuClockMhz = gpuClockMhz;
            IsGpuClockAvailable = true;
            IsGpuAvailable = true;
        }

        if (reading.GpuTempC is int gpuTempC && gpuTempC > 0 && gpuTempC < 150)
        {
            GpuTempC = gpuTempC;
            IsGpuTempAvailable = true;
            IsGpuAvailable = true;
        }
    }

    // ── HWiNFO Shared Memory fallback (HVCI-kompatibel) ─────────────────

    private void PollHwInfo()
    {
        // Only skip HWiNFO when LHM actually delivers temperature data.
        // LHM may have sensors (clock etc.) but no temps due to HVCI blocking ring0 driver.
        if (_libreHardwareSensor.ProvidesCpuTemperature)
            return;

        if (!_hwInfoAvailable)
            return;

        // Don't run until LHM init is done
        if (!_libreHardwareSensor.InitializationDone)
            return;

        // Poll every ~4 seconds
        if (_hwInfoSkip++ % 2 != 0)
            return;

        try
        {
            _hwInfoProvidesTemp = false;
            var result = HwInfoSharedMemoryReader.Read();
            if (result.Status == HwInfoReadStatus.InvalidSignature)
            {
                _hwInfoAvailable = false;
                Log("HWiNFO: Shared Memory Signatur ungueltig");
                return;
            }

            if (result.Status == HwInfoReadStatus.NoData)
            {
                if (!_hwInfoLogged) { Log("HWiNFO: Keine Sensordaten in Shared Memory"); _hwInfoLogged = true; }
                return;
            }

            if (!_hwInfoLogged)
            {
                Log($"HWiNFO: Shared Memory aktiv — {result.TotalReadingCount} Messwerte");
                _hwInfoLogged = true;
            }

            var sensors = HwInfoSensorSelector.Select(result.Readings);
            if (sensors.CpuTempC.HasValue)
            {
                SetCpuTempReading(sensors.CpuTempC.Value, "HWiNFO Shared Memory");
            }
            if (sensors.GpuTempC.HasValue)
            {
                GpuTempC = sensors.GpuTempC.Value;
                IsGpuTempAvailable = true;
            }
            if (sensors.CpuClockMhz.HasValue)
            {
                CpuClockMhz = sensors.CpuClockMhz.Value;
                IsCpuClockAvailable = true;
            }
            if (sensors.GpuClockMhz.HasValue)
            {
                GpuClockMhz = sensors.GpuClockMhz.Value;
                IsGpuClockAvailable = true;
            }
            if (sensors.RamClockMhz.HasValue)
            {
                RamClockMhz = sensors.RamClockMhz.Value;
                IsRamClockAvailable = true;
            }

            // Clear sensor-blocked state since HWiNFO is providing data
            if (sensors.CpuTempC.HasValue || sensors.GpuTempC.HasValue)
            {
                IsSensorBlocked = false;
                SensorBlockedReason = "";
            }

            _hwInfoProvidesTemp = sensors.CpuTempC.HasValue;
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

                SetCpuTempUnavailable("CPU-Temperatur nicht verfügbar: HWiNFO Shared Memory ist nicht aktiv oder LHM liefert keinen CPU-Temperatursensor.");
            }
        }
        catch (Exception ex)
        {
            if (!_hwInfoLogged)
            {
                Log($"HWiNFO: Fehler — {ex.GetType().Name}: {ex.Message}");
                _hwInfoLogged = true;
                _hwInfoAvailable = false;
                SetCpuTempUnavailable("CPU-Temperatur nicht verfügbar: HWiNFO Shared Memory konnte nicht gelesen werden.");
            }
        }
    }

    // ── CPU temp fallback via WMI/PerfCounter (wenn LHM fehlschlaegt) ───

    // Stage 1: PerfCounter (kein Admin noetig, Win10+)
    private volatile bool _perfCounterTempAvailable = true;
    private int _perfCounterTempFailCount;

    private void PollCpuTempFallback()
    {
        // Nur Fallback nutzen, wenn keine LIVE-Quelle (LHM/HWiNFO) die CPU-Temp liefert.
        // NICHT auf IsCpuTempAvailable pruefen - das setzt der Fallback selbst und wuerde
        // den Thermal-Zone-Wert nach der ersten Messung einfrieren (Bug-Fix).
        if (_libreHardwareSensor.ProvidesCpuTemperature || _hwInfoProvidesTemp)
            return;

        // Don't run until LHM init is complete (give LHM a chance first)
        if (!_libreHardwareSensor.InitializationDone)
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
            var result = ExternalProcessRunner.RunAsync(
                "powershell.exe",
                ["-NoProfile", "-NoLogo", "-Command", "$z = Get-CimInstance Win32_PerfFormattedData_Counters_ThermalZoneInformation -ErrorAction SilentlyContinue | Sort-Object Temperature -Descending | Select-Object -First 1; if($z -and $z.Temperature -gt 200){[math]::Round($z.Temperature - 273.15)}else{'0'}"],
                TimeSpan.FromSeconds(5),
                Encoding.UTF8,
                Encoding.UTF8).GetAwaiter().GetResult();
            if (!result.Success) return;

            var output = result.StdOut.Trim();

            if (int.TryParse(output, NumberStyles.Integer, CultureInfo.InvariantCulture, out var celsius)
                && celsius > 0 && celsius < 150)
            {
                _perfCounterTempFailCount = 0;
                _dispatcher.BeginInvoke(() =>
                {
                    SetCpuTempReading(celsius, "Windows Thermal Zone");
                });

                if (_wmiTempSkip <= 6)
                    Log($"PerfCounter CPU-Temp: {celsius} °C (kein Admin noetig)");
                return;
            }

            if (Interlocked.Increment(ref _perfCounterTempFailCount) >= 3)
            {
                _perfCounterTempAvailable = false;
                Log("PerfCounter CPU-Temp: nicht verfuegbar, versuche ACPI Fallback...");
                SetCpuTempUnavailable("CPU-Temperatur noch nicht verfügbar: Windows Thermal-Zone-Fallback wird geprüft.");
            }
        }
        catch
        {
            if (Interlocked.Increment(ref _perfCounterTempFailCount) >= 3)
            {
                _perfCounterTempAvailable = false;
                Log("PerfCounter CPU-Temp: fehlgeschlagen, versuche ACPI Fallback...");
                SetCpuTempUnavailable("CPU-Temperatur noch nicht verfügbar: Windows Thermal-Zone-Fallback wird geprüft.");
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
            var result = ExternalProcessRunner.RunAsync(
                "powershell.exe",
                ["-NoProfile", "-NoLogo", "-Command", "$t = Get-CimInstance -Namespace root/WMI -ClassName MSAcpi_ThermalZoneTemperature -ErrorAction SilentlyContinue | Select-Object -First 1; if($t){$t.CurrentTemperature}else{'0'}"],
                TimeSpan.FromSeconds(5),
                Encoding.UTF8,
                Encoding.UTF8).GetAwaiter().GetResult();
            if (!result.Success) return;

            var output = result.StdOut.Trim();

            if (int.TryParse(output, NumberStyles.Integer, CultureInfo.InvariantCulture, out var raw) && raw > 0)
            {
                // MSAcpi_ThermalZoneTemperature returns temp in tenths of Kelvin
                var celsius = (int)Math.Round((raw / 10.0) - 273.15);
                if (celsius > 0 && celsius < 150)
                {
                    _wmiTempFailCount = 0;
                    _dispatcher.BeginInvoke(() =>
                    {
                        SetCpuTempReading(celsius, "Windows Thermal Zone");
                    });

                    if (_wmiTempSkip <= 6)
                        Log($"ACPI CPU-Temp Fallback: {celsius} °C (Admin-Modus)");
                    return;
                }
            }

            if (Interlocked.Increment(ref _wmiTempFailCount) >= 3)
            {
                _wmiTempAvailable = false;
                Log("ACPI CPU-Temp: nicht verfuegbar auf diesem System");
                SetCpuTempUnavailable("CPU-Temperatur nicht verfügbar: Windows liefert keinen nutzbaren CPU-Thermalsensor.");
            }
        }
        catch
        {
            if (Interlocked.Increment(ref _wmiTempFailCount) >= 3)
            {
                _wmiTempAvailable = false;
                Log("ACPI CPU-Temp: PowerShell-Abfrage fehlgeschlagen");
                SetCpuTempUnavailable("CPU-Temperatur nicht verfügbar: Windows-Thermalzone konnte nicht gelesen werden.");
            }
        }
    }

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

        var pct = CpuDeltaCalculator.ComputePercent(deltaIdle, deltaTotal);
        if (pct.HasValue)
            CpuPercent = pct.Value;
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

    private void PollGpu()
    {
        if (!_gpuAvailable)
            return;

        // Query GPU less frequently (every other tick = ~4s) to reduce overhead
        if (_gpuQuerySkip++ % 2 != 0)
            return;

        Task.Run(() =>
        {
            if (Volatile.Read(ref _disposed) != 0)
                return;

            try
            {
                var result = ExternalProcessRunner.RunAsync(
                    _nvidiaSmiPath!,
                    ["--query-gpu=utilization.gpu,memory.used,memory.total,temperature.gpu,clocks.current.graphics,name", "--format=csv,noheader,nounits"],
                    TimeSpan.FromSeconds(3),
                    Encoding.UTF8,
                    Encoding.UTF8).GetAwaiter().GetResult();
                if (!result.Success) return;

                var output = result.StdOut;

                // Parse "82, 4521, 12288, 65, 1920, NVIDIA GeForce RTX 4070"
                var reading = NvidiaSmiOutputParser.Parse(output);
                if (reading is null)
                {
                    if (_gpuQuerySkip <= 4)
                        Log($"nvidia-smi: unerwartete Ausgabe: '{output.Trim()}'");
                    return;
                }

                _gpuFailCount = 0; // reset on success

                _dispatcher.BeginInvoke(() =>
                {
                    if (Volatile.Read(ref _disposed) != 0)
                        return;

                    GpuPercent = reading.GpuPercent;
                    GpuMemUsedMb = reading.MemUsedMb;
                    GpuMemTotalMb = reading.MemTotalMb;
                    GpuMemPercent = NvidiaSmiOutputParser.ComputeMemPercent(reading.MemUsedMb, reading.MemTotalMb);
                    if (reading.TempC.HasValue)
                    {
                        GpuTempC = reading.TempC.Value;
                        IsGpuTempAvailable = true;
                    }
                    if (reading.ClockMhz.HasValue)
                    {
                        GpuClockMhz = reading.ClockMhz.Value;
                        IsGpuClockAvailable = true;
                    }
                    if (reading.GpuName.Length > 0)
                        GpuName = reading.GpuName;
                    IsGpuAvailable = true;
                });
            }
            catch (Exception ex)
            {
                // Only permanently disable after 5 consecutive failures
                if (Interlocked.Increment(ref _gpuFailCount) >= 5)
                {
                    _gpuAvailable = false;
                    Log($"nvidia-smi: deaktiviert nach 5 Fehlern ({ex.Message})");
                }
            }
        });
    }

    private static string? FindNvidiaSmi()
    {
        // Check System32 first (modern NVIDIA drivers install here)
        var sys32 = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "nvidia-smi.exe");
        if (File.Exists(sys32))
            return sys32;

        // Legacy NVSMI folder
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var nvsmi = Path.Combine(programFiles, "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe");
        if (File.Exists(nvsmi))
            return nvsmi;

        // Try via NVIDIA driver folder (some installations)
        var nvidiaDriver = Path.Combine(programFiles, "NVIDIA Corporation", "NVIDIA NVS", "nvidia-smi.exe");
        if (File.Exists(nvidiaDriver))
            return nvidiaDriver;

        // Fallback: try from PATH
        try
        {
            var result = ExternalProcessRunner.RunAsync(
                "nvidia-smi",
                ["--version"],
                TimeSpan.FromSeconds(3),
                Encoding.UTF8,
                Encoding.UTF8).GetAwaiter().GetResult();
            if (result.Success)
                return "nvidia-smi";
        }
        catch { /* not in PATH */ }

        return null;
    }

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
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _timer.Stop();
        _timer.Tick -= OnTick;
        _libreHardwareSensor.Dispose();
    }
}
