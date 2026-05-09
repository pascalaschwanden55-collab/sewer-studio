using System;
using System.Diagnostics;
using System.Globalization;

namespace AuswertungPro.Next.UI.Services;

// SystemMonitorService CPU-Temperatur-Fallback-Strategien:
// 1. PerfCounter (Thermal Zone Information / CpuTempPerfCounter)
// 2. WMI/ACPI MSAcpi_ThermalZoneTemperature via PowerShell
// 3. Fallback wenn LibreHardwareMonitor keine Temp liefert.
// Aus dem Hauptdatei extrahiert (Slice 12b).
public sealed partial class SystemMonitorService
{
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
}
