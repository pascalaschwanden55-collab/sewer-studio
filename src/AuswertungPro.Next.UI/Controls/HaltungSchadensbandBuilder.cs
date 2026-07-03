using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>Ein Marker auf dem Haltungs-Schadensband.</summary>
public sealed record SchadensbandMarker(
    double Meter,
    double? MeterEnd,
    string Code,
    string Beschreibung,
    MarkerColorKind Farbe,
    object Quelle);

/// <summary>Daten fuer das Schadensband einer Haltung.</summary>
public sealed record SchadensbandDaten(
    double TotalLength,
    IReadOnlyList<SchadensbandMarker> Marker);

/// <summary>
/// Baut aus den Protokolleintraegen einer Haltung die Marker fuers Schadensband
/// (WinCan-artige Laengsansicht in der Haltungsansicht). Farbe nach Code-Gruppe:
/// BA=strukturell (rot), BB=betrieblich (gelb), BC=Grundgeruest (gruen),
/// Rest (BD/AE/leer) neutral grau. Streckenschaeden liefern MeterEnd.
/// </summary>
public static class HaltungSchadensbandBuilder
{
    private static readonly SchadensbandDaten Leer =
        new(0d, Array.Empty<SchadensbandMarker>());

    public static SchadensbandDaten Build(HaltungRecord? record)
    {
        if (record is null)
            return Leer;

        var entries = record.Protocol?.Current.Entries;
        var marker = new List<SchadensbandMarker>();

        if (entries is not null)
        {
            foreach (var entry in entries)
            {
                if (entry.MeterStart is not double meter)
                    continue;

                double? ende = entry.MeterEnd is double e && e > meter ? e : null;
                var code = (entry.Code ?? string.Empty).Trim();

                marker.Add(new SchadensbandMarker(
                    meter,
                    ende,
                    code,
                    entry.Beschreibung ?? string.Empty,
                    FarbeFuerCode(code),
                    entry));
            }
        }

        marker.Sort((a, b) => a.Meter.CompareTo(b.Meter));

        return new SchadensbandDaten(ErmittleLaenge(record, marker), marker);
    }

    /// <summary>Laenge aus Haltungslaenge_m; bei leer/0 der letzte Meterstand der Eintraege.</summary>
    private static double ErmittleLaenge(HaltungRecord record, List<SchadensbandMarker> marker)
    {
        var roh = (record.GetFieldValue("Haltungslaenge_m") ?? string.Empty).Trim().Replace(',', '.');
        if (double.TryParse(roh, NumberStyles.Float, CultureInfo.InvariantCulture, out var laenge) && laenge > 0d)
            return laenge;

        return marker.Count == 0
            ? 0d
            : marker.Max(m => m.MeterEnd ?? m.Meter);
    }

    /// <summary>VSA-Code-Gruppe → Bandfarbe (BA rot, BB gelb, BC gruen, Rest grau).</summary>
    private static MarkerColorKind FarbeFuerCode(string code)
    {
        if (code.StartsWith("BA", StringComparison.OrdinalIgnoreCase))
            return MarkerColorKind.Red;
        if (code.StartsWith("BB", StringComparison.OrdinalIgnoreCase))
            return MarkerColorKind.Yellow;
        if (code.StartsWith("BC", StringComparison.OrdinalIgnoreCase))
            return MarkerColorKind.Green;
        return MarkerColorKind.Rejected;
    }
}
