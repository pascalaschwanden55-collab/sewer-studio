using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Kapselt Initialisierung und Auswertung von LibreHardwareMonitor.
/// Der SystemMonitorService uebernimmt weiterhin Taktung und UI-Zustand.
/// </summary>
internal sealed class LibreHardwareMonitorSensor : IDisposable
{
    private readonly bool _enabled;
    private readonly object _gate = new();
    private Computer? _computer;
    private volatile bool _initializationDone;
    private bool _retried;
    private bool _hvciChecked;
    private bool _isHvciEnabled;
    private int _pollSkip;
    private int _disposed;

    public LibreHardwareMonitorSensor(bool enabled)
    {
        _enabled = enabled;
    }

    public bool ProvidesCpuTemperature { get; private set; }
    public bool InitializationDone => _initializationDone;

    public LibreHardwareInitializationResult Initialize()
    {
        if (!_enabled || Volatile.Read(ref _disposed) != 0)
            return LibreHardwareInitializationResult.Skipped;

        var messages = new List<string>();
        Computer? openedComputer = null;

        try
        {
            if (!_hvciChecked)
            {
                _hvciChecked = true;
                _isHvciEnabled = DetectHvci();
                if (_isHvciEnabled)
                    messages.Add("HVCI: Memory Integrity ist AKTIV — Hardware-Sensortreiber blockiert");
            }

            messages.Add("LHM: Initialisierung gestartet...");
            openedComputer = new Computer
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
            openedComputer.Open();
            messages.Add("LHM: computer.Open() erfolgreich");

            foreach (var root in openedComputer.Hardware)
                UpdateHardwareTree(root);

            var totalSensors = 0;
            foreach (var hardware in EnumerateHardwareTree(openedComputer))
            {
                var sensorCount = hardware.Sensors.Length;
                totalSensors += sensorCount;

                if (!IsMonitoredHardwareType(hardware.HardwareType))
                    continue;

                messages.Add($"LHM: {hardware.HardwareType} '{hardware.Name}' — {sensorCount} Sensoren");
                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType is SensorType.Temperature or SensorType.Clock)
                    {
                        messages.Add(
                            $"  -> {sensor.SensorType}: {sensor.Name} = {sensor.Value?.ToString("F1") ?? "null"}");
                    }
                }
            }

            var hasAnySensors = EnumerateHardwareTree(openedComputer)
                .Any(hardware => IsMonitoredHardwareType(hardware.HardwareType)
                                 && hardware.Sensors.Length > 0);

            if (!hasAnySensors)
            {
                openedComputer.Close();
                openedComputer = null;
                var reason = _isHvciEnabled
                    ? "HVCI (Kernisolierung) blockiert Sensor-Treiber.\nOption 1: HWiNFO64 starten (Shared Memory aktivieren) — funktioniert mit HVCI.\nOption 2: Kernisolierung deaktivieren unter Windows-Sicherheit > Geraetesicherheit."
                    : "Keine Sensoren gefunden (Admin-Rechte? Treiber?)";
                messages.Add($"LHM: FEHLGESCHLAGEN — {reason.Replace('\n', ' ')}");
                return new LibreHardwareInitializationResult(
                    false,
                    _isHvciEnabled,
                    reason,
                    "CPU-Temperatur nicht verfügbar: Sensorzugriff blockiert oder kein Hardware-Sensor gefunden.",
                    messages);
            }

            if (Volatile.Read(ref _disposed) != 0)
            {
                openedComputer.Close();
                return LibreHardwareInitializationResult.Skipped;
            }

            lock (_gate)
            {
                _computer?.Close();
                _computer = openedComputer;
                openedComputer = null;
            }

            messages.Add($"LHM: OK — {totalSensors} Sensoren aktiv");
            return new LibreHardwareInitializationResult(true, _isHvciEnabled, null, null, messages);
        }
        catch (Exception ex)
        {
            BestEffortClose(openedComputer);
            var reason = _isHvciEnabled
                ? "HVCI blockiert Sensor-Treiber"
                : $"Sensor-Fehler: {ex.Message}";
            messages.Add($"LHM: EXCEPTION — {ex.GetType().Name}: {ex.Message}");
            return new LibreHardwareInitializationResult(
                false,
                _isHvciEnabled,
                reason,
                "CPU-Temperatur nicht verfügbar: Sensorzugriff fehlgeschlagen.",
                messages);
        }
        finally
        {
            _initializationDone = true;
        }
    }

    public LibreHardwarePollResult Poll()
    {
        if (!_enabled || Volatile.Read(ref _disposed) != 0)
            return LibreHardwarePollResult.Empty;

        Computer? computer;
        lock (_gate)
            computer = _computer;

        if (computer is null)
        {
            if (_initializationDone && !_retried && _pollSkip++ > 15)
            {
                _retried = true;
                return LibreHardwarePollResult.Retry;
            }

            return LibreHardwarePollResult.Empty;
        }

        if (_pollSkip++ % 2 != 0)
            return LibreHardwarePollResult.Empty;

        try
        {
            foreach (var root in computer.Hardware)
                UpdateHardwareTree(root);

            var hardwareTree = EnumerateHardwareTree(computer).ToList();
            var gpuName = hardwareTree
                .Where(hardware => IsGpuHardwareType(hardware.HardwareType))
                .Select(hardware => hardware.Name?.Trim())
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
            var samples = hardwareTree
                .SelectMany(hardware => hardware.Sensors
                    .Where(sensor => sensor.Value.HasValue)
                    .Select(sensor => new LibreHardwareSensorSample(
                        hardware.HardwareType,
                        hardware.Name ?? string.Empty,
                        sensor.SensorType,
                        sensor.Name ?? string.Empty,
                        sensor.Value!.Value)))
                .ToList();

            var reading = Select(samples);
            if (!string.IsNullOrWhiteSpace(gpuName))
                reading = reading with { GpuName = gpuName };
            if (reading.CpuTempC is int cpuTempC && cpuTempC > 0 && cpuTempC < 150)
                ProvidesCpuTemperature = true;
            return new LibreHardwarePollResult(false, null, reading);
        }
        catch (Exception ex)
        {
            return new LibreHardwarePollResult(
                false,
                $"LHM Poll: {ex.GetType().Name}: {ex.Message}",
                null);
        }
    }

    internal static LibreHardwareReading Select(IEnumerable<LibreHardwareSensorSample> samples)
    {
        var cpuTempC = 0;
        var cpuClockMhz = 0;
        var cpuTempFound = false;
        var cpuClockFound = false;
        var boardCpuTempC = 0;
        var boardCpuTempFound = false;

        var ramClockMhz = 0;
        var ramTempC = 0;
        var ramTempFound = false;
        var ramClockFound = false;
        var boardRamTempC = 0;
        var boardRamTempFound = false;

        var gpuLoadPercent = 0;
        var gpuClockMhz = 0;
        var gpuTempC = 0;
        var gpuLoadFound = false;
        var gpuClockFound = false;
        var gpuTempFound = false;
        string? gpuName = null;

        foreach (var sample in samples)
        {
            if (sample.HardwareType == HardwareType.Cpu)
            {
                if (sample.SensorType == SensorType.Temperature)
                {
                    var temperature = (int)Math.Round(sample.Value);
                    if (!cpuTempFound
                        || sample.SensorName.Contains("Package", StringComparison.OrdinalIgnoreCase)
                        || temperature > cpuTempC)
                    {
                        cpuTempC = temperature;
                        cpuTempFound = true;
                    }
                }

                if (sample.SensorType == SensorType.Clock
                    && (sample.SensorName.Contains("Core", StringComparison.OrdinalIgnoreCase)
                        || sample.SensorName.Contains("CPU", StringComparison.OrdinalIgnoreCase)))
                {
                    var clock = (int)Math.Round(sample.Value);
                    if (!cpuClockFound || clock > cpuClockMhz)
                    {
                        cpuClockMhz = clock;
                        cpuClockFound = true;
                    }
                }
            }
            else if (sample.HardwareType == HardwareType.Memory)
            {
                if (sample.SensorType == SensorType.Clock && (int)sample.Value > ramClockMhz)
                {
                    ramClockMhz = (int)Math.Round(sample.Value);
                    ramClockFound = true;
                }

                if (sample.SensorType == SensorType.Temperature)
                {
                    ramTempC = (int)Math.Round(sample.Value);
                    ramTempFound = true;
                }
            }
            else if (IsGpuHardwareType(sample.HardwareType))
            {
                if (string.IsNullOrWhiteSpace(gpuName) && !string.IsNullOrWhiteSpace(sample.HardwareName))
                    gpuName = sample.HardwareName.Trim();

                if (sample.SensorType == SensorType.Load)
                {
                    var load = (int)Math.Round(sample.Value);
                    if (!gpuLoadFound
                        || sample.SensorName.Contains("Core", StringComparison.OrdinalIgnoreCase)
                        || sample.SensorName.Contains("GPU", StringComparison.OrdinalIgnoreCase)
                        || sample.SensorName.Contains("3D", StringComparison.OrdinalIgnoreCase)
                        || load > gpuLoadPercent)
                    {
                        gpuLoadPercent = load;
                        gpuLoadFound = true;
                    }
                }

                if (sample.SensorType == SensorType.Clock)
                {
                    var clock = (int)Math.Round(sample.Value);
                    if (!gpuClockFound
                        || sample.SensorName.Contains("Core", StringComparison.OrdinalIgnoreCase)
                        || sample.SensorName.Contains("Graphics", StringComparison.OrdinalIgnoreCase)
                        || sample.SensorName.Contains("GPU", StringComparison.OrdinalIgnoreCase)
                        || clock > gpuClockMhz)
                    {
                        gpuClockMhz = clock;
                        gpuClockFound = true;
                    }
                }

                if (sample.SensorType == SensorType.Temperature)
                {
                    var temperature = (int)Math.Round(sample.Value);
                    if (!gpuTempFound || temperature > gpuTempC)
                    {
                        gpuTempC = temperature;
                        gpuTempFound = true;
                    }
                }
            }
            else if (sample.HardwareType is HardwareType.Motherboard or HardwareType.SuperIO
                     && sample.SensorType == SensorType.Temperature)
            {
                var temperature = (int)Math.Round(sample.Value);
                if (temperature <= 0 || temperature >= 150)
                    continue;

                if (sample.SensorName.Contains("CPU", StringComparison.OrdinalIgnoreCase)
                    || sample.SensorName.Contains("Package", StringComparison.OrdinalIgnoreCase)
                    || sample.SensorName.Contains("Tctl", StringComparison.OrdinalIgnoreCase)
                    || sample.SensorName.Contains("Die", StringComparison.OrdinalIgnoreCase))
                {
                    if (!boardCpuTempFound || temperature > boardCpuTempC)
                    {
                        boardCpuTempC = temperature;
                        boardCpuTempFound = true;
                    }
                }

                if (sample.SensorName.Contains("RAM", StringComparison.OrdinalIgnoreCase)
                    || sample.SensorName.Contains("DRAM", StringComparison.OrdinalIgnoreCase)
                    || sample.SensorName.Contains("DIMM", StringComparison.OrdinalIgnoreCase)
                    || sample.SensorName.Contains("Memory", StringComparison.OrdinalIgnoreCase))
                {
                    if (!boardRamTempFound || temperature > boardRamTempC)
                    {
                        boardRamTempC = temperature;
                        boardRamTempFound = true;
                    }
                }
            }
        }

        if (!cpuTempFound && boardCpuTempFound)
        {
            cpuTempC = boardCpuTempC;
            cpuTempFound = true;
        }

        if (!ramTempFound && boardRamTempFound)
        {
            ramTempC = boardRamTempC;
            ramTempFound = true;
        }

        return new LibreHardwareReading(
            cpuTempFound ? cpuTempC : null,
            cpuClockFound ? cpuClockMhz : null,
            ramTempFound ? ramTempC : null,
            ramClockFound ? ramClockMhz : null,
            gpuLoadFound ? gpuLoadPercent : null,
            gpuClockFound ? gpuClockMhz : null,
            gpuTempFound ? gpuTempC : null,
            gpuName);
    }

    private static bool DetectHvci()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity");
            return HvciDetector.IsEnabled(key?.GetValue("Enabled"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[SystemMonitor] HVCI-Status nicht lesbar: {ex.GetType().Name}");
            return false;
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
        foreach (var subHardware in hardware.SubHardware)
        {
            foreach (var item in EnumerateHardwareTree(subHardware))
                yield return item;
        }
    }

    private static void UpdateHardwareTree(IHardware hardware)
    {
        hardware.Update();
        foreach (var subHardware in hardware.SubHardware)
            UpdateHardwareTree(subHardware);
    }

    private static bool IsMonitoredHardwareType(HardwareType hardwareType)
        => hardwareType is HardwareType.Cpu
            or HardwareType.Memory
            or HardwareType.Motherboard
            or HardwareType.SuperIO
           || IsGpuHardwareType(hardwareType);

    private static bool IsGpuHardwareType(HardwareType hardwareType)
        => hardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;

    private static void BestEffortClose(Computer? computer)
    {
        if (computer is null)
            return;
        try
        {
            computer.Close();
        }
        catch
        {
            // Schliessen darf Initialisierungsfehler nicht verdecken.
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        lock (_gate)
        {
            BestEffortClose(_computer);
            _computer = null;
        }
    }
}

internal sealed record LibreHardwareInitializationResult(
    bool Succeeded,
    bool IsHvciEnabled,
    string? FailureReason,
    string? TemperatureUnavailableReason,
    IReadOnlyList<string> Messages)
{
    public static LibreHardwareInitializationResult Skipped { get; } =
        new(false, false, null, null, Array.Empty<string>());
}

internal sealed record LibreHardwarePollResult(
    bool RetryRequested,
    string? Error,
    LibreHardwareReading? Reading)
{
    public static LibreHardwarePollResult Empty { get; } = new(false, null, null);
    public static LibreHardwarePollResult Retry { get; } = new(true, null, null);
}

internal sealed record LibreHardwareReading(
    int? CpuTempC,
    int? CpuClockMhz,
    int? RamTempC,
    int? RamClockMhz,
    int? GpuLoadPercent,
    int? GpuClockMhz,
    int? GpuTempC,
    string? GpuName);

internal sealed record LibreHardwareSensorSample(
    HardwareType HardwareType,
    string HardwareName,
    SensorType SensorType,
    string SensorName,
    float Value);
