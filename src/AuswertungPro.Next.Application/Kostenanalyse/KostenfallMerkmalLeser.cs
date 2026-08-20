using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Kostenanalyse;

/// <summary>
/// Liest die Merkmale einer Haltung: Schadensarten mit Anzahl, Durchmesser, Laenge,
/// Boegen und seitliche Anschluesse.
///
/// Bauteile sind keine Schaeden: BCD (Rohranfang), BCE (Rohrende), BDA und 000M kommen
/// in praktisch jeder Haltung vor und wuerden jede Aehnlichkeit verwaessern. BCA
/// (seitlicher Anschluss) ist ebenfalls kein Schaden, aber ein Mengentreiber — jeder
/// Anschluss muss nach dem Linern geoeffnet und eingebunden werden — und wird darum
/// getrennt gezaehlt.
/// </summary>
public static class KostenfallMerkmalLeser
{
    private static readonly HashSet<string> Bauteile =
        new(StringComparer.OrdinalIgnoreCase) { "BCD", "BCE", "BDA", "000M" };

    private const string AnschlussCode = "BCA";
    private const string BogenCode = "BCC";

    public static KostenfallMerkmale Lies(HaltungRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var schaeden = new Dictionary<string, (int Anzahl, bool Strecke)>(StringComparer.OrdinalIgnoreCase);
        var anschluesse = 0;
        var boegen = 0;

        foreach (var eintrag in record.Protocol?.Current?.Entries ?? [])
        {
            if (eintrag.IsDeleted)
                continue;

            var code = (eintrag.Code ?? string.Empty).Trim().ToUpperInvariant();
            if (code.Length < 3)
                continue;

            var hauptcode = code[..3];

            if (code.StartsWith(BogenCode, StringComparison.Ordinal))
            {
                boegen++;
                continue;
            }

            if (hauptcode == AnschlussCode)
            {
                anschluesse++;
                continue;
            }

            if (Bauteile.Contains(hauptcode) || Bauteile.Contains(code))
                continue;

            var vorher = schaeden.TryGetValue(hauptcode, out var wert) ? wert : (0, false);
            schaeden[hauptcode] = (vorher.Item1 + 1, vorher.Item2 || eintrag.IsStreckenschaden);
        }

        return new KostenfallMerkmale
        {
            DnMm = LiesGanzzahl(record.GetFieldValue(FieldKeys.NominalDiameterMm)),
            LaengeM = LiesZahl(record.GetFieldValue(FieldKeys.HoldingLengthMeters)) ?? 0d,
            BogenAnzahl = boegen,
            AnschlussAnzahl = anschluesse,
            Schaeden = schaeden
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => new SchadensMerkmal(kv.Key, kv.Value.Anzahl, kv.Value.Strecke))
                .ToList()
        };
    }

    private static int? LiesGanzzahl(string? text)
        => int.TryParse((text ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var wert)
            ? wert
            : null;

    private static double? LiesZahl(string? text)
    {
        // Punkt und Komma gleich behandeln — nie ueber CurrentCulture.
        var roh = (text ?? "").Trim().Replace(',', '.');
        return double.TryParse(roh, NumberStyles.Float, CultureInfo.InvariantCulture, out var wert)
            ? wert
            : null;
    }
}
