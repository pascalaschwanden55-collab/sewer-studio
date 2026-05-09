using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using LibreHardwareMonitor.Hardware;

namespace AuswertungPro.Next.UI.Services;

// SystemMonitorService Hardware-Sensor-Polling: PollHardwareMonitor (LibreHardware
// Monitor: CPU/GPU/Mainboard) + PollHwInfo (HWiNFO Shared-Memory-Fallback,
// HVCI-kompatibel ohne Admin-Rechte). Aus dem Hauptdatei extrahiert (Slice 12c).
public sealed partial class SystemMonitorService
{
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

}
