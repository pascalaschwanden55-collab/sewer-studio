using System.Globalization;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Objektakten;

/// <summary>Nur die im WebGIS belegten Eingaberegeln; Normprüfung folgt zusätzlich beim Export.</summary>
public static class ObjektFeldPruefung
{
    public static void Pruefe(ObjektFeldDefinition feld, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            if (feld.WebgisPflicht) throw new InvalidOperationException($"«{feld.Label}» ist ein Pflichtfeld.");
            return;
        }
        if (feld.WebgisMaxLaenge is > 0 && text.Length > feld.WebgisMaxLaenge)
            throw new InvalidOperationException($"«{feld.Label}» erlaubt höchstens {feld.WebgisMaxLaenge} Zeichen.");
        if (feld.WebgisFeldart == "datum" && !DateOnly.TryParseExact(text.Trim(),
                ["yyyyMMdd", "yyyy-MM-dd", "dd.MM.yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new InvalidOperationException($"«{feld.Label}»: Bitte ein gültiges Datum eingeben, zum Beispiel 12.09.2026.");
        if (feld.WebgisFeldart == "zahl" && (!double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var zahl) || !double.IsFinite(zahl)))
            throw new InvalidOperationException($"«{feld.Label}»: Bitte eine gültige Zahl eingeben.");
    }
}
