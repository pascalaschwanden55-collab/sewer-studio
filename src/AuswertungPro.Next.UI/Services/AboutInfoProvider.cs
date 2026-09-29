using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 5 («Programmidentitaet»): die reine, WPF-freie
/// Zusammenstellung der Angaben fuer das Fenster «Über SewerStudio». Programmname und Version
/// kommen aus <see cref="AppIdentity"/> (einzige Quelle der Wahrheit), die Ordner aus
/// <see cref="AppSettings.AppDataDir"/> (keine hartkodierten Pfade). Ein Git-Commit wird bewusst
/// NICHT ausgewiesen - im Projekt existiert dafuer kein Assembly-Attribut
/// (<c>AssemblyMetadata</c>/<c>InformationalVersion</c> mit Commit-Hash); ihn neu einzufuehren
/// waere kein "ohne Aufwand verfuegbarer" Wert im Sinn der Aufgabenbeschreibung, sondern neue
/// Build-Infrastruktur. Das Build-Datum kommt stattdessen ohne neue Infrastruktur aus dem
/// Schreibzeitpunkt der ausgefuehrten Assembly-Datei.
/// </summary>
public sealed record AboutSystemInfo(
    string ProduktName,
    string Version,
    string? BuildDatum,
    string Betriebssystem,
    string DotNetVersion,
    string? GpuName,
    string DatenOrdner,
    string ProtokollOrdner,
    string EinstellungenOrdner)
{
    /// <summary>Klartext fuer «Systeminfo kopieren» - dieselben Angaben, die auch im Fenster stehen.</summary>
    public string AlsKopierbarerText()
    {
        var zeilen = new List<string> { $"{ProduktName} {Version}" };
        if (!string.IsNullOrWhiteSpace(BuildDatum))
            zeilen.Add($"Build: {BuildDatum}");
        zeilen.Add($"Windows: {Betriebssystem}");
        zeilen.Add($".NET: {DotNetVersion}");
        if (!string.IsNullOrWhiteSpace(GpuName))
            zeilen.Add($"GPU: {GpuName}");
        return string.Join(Environment.NewLine, zeilen);
    }
}

public static class AboutInfoProvider
{
    /// <summary>
    /// Baut die Angaben. <paramref name="gpuName"/> kommt vom Aufrufer (Grafikkartenname eines
    /// bereits laufenden, synchron abfragbaren Diensts, z. B. <c>SystemMonitorService.GpuName</c>);
    /// dieser Provider startet selbst keinen Sensor/Dienst. Ein leerer/unbekannter Name blendet die
    /// GPU-Zeile aus statt einen erfundenen Wert zu zeigen.
    /// </summary>
    public static AboutSystemInfo Erstelle(string? gpuName = null)
        => new(
            ProduktName: AppIdentity.ProductName,
            Version: AppIdentity.DisplayVersion,
            BuildDatum: ErmittleBuildDatum(),
            Betriebssystem: RuntimeInformation.OSDescription,
            DotNetVersion: RuntimeInformation.FrameworkDescription,
            GpuName: string.IsNullOrWhiteSpace(gpuName) ? null : gpuName,
            DatenOrdner: AppSettings.AppDataDir,
            ProtokollOrdner: Path.Combine(AppSettings.AppDataDir, "logs"),
            // settings.json liegt direkt im Datenordner (AppSettings.SettingsPath); ein eigener
            // Unterordner fuer Einstellungen existiert nicht - ehrlich denselben Ordner zeigen statt
            // einen zweiten, in Wahrheit identischen Pfad zu erfinden.
            EinstellungenOrdner: AppSettings.AppDataDir);

    private static string? ErmittleBuildDatum()
    {
        try
        {
            var pfad = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrWhiteSpace(pfad) || !File.Exists(pfad))
                return null;

            return File.GetLastWriteTime(pfad).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }
}
