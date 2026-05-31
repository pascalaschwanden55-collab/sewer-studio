namespace AuswertungPro.Next.Infrastructure.Map;

public enum ZustandFarbe { Unbekannt, Gut, Mittel, Schlecht }

/// <summary>
/// Normalisiert einen Zustand-Rohwert eindeutig auf eine Farbstufe.
/// VSA-Skala: 0 = bester, hoeher = schlechter. 'invertiert' kehrt das um
/// (EZ-Skala 0=schlecht/4=gut), damit die Karte nie falsch herum faerbt.
/// </summary>
public static class ZustandColorMapper
{
    public static ZustandFarbe Map(int? wert, bool invertiert)
    {
        if (wert is null) return ZustandFarbe.Unbekannt;

        if (invertiert)
        {
            // EZ-Skala 0-4: 4 = bester, 0 = schlechtester Zustand.
            // Eigene Schwellen, damit die Mittelklasse (2) nicht faelschlich
            // als Schlecht (rot) erscheint (frueher: 5 - wert).
            return wert.Value switch
            {
                >= 3 => ZustandFarbe.Gut,      // 4, 3
                2 => ZustandFarbe.Mittel,      // Mittelklasse
                _ => ZustandFarbe.Schlecht,    // 1, 0
            };
        }

        // VSA-Skala: 0 = bester, hoeher = schlechter.
        return wert.Value switch
        {
            <= 1 => ZustandFarbe.Gut,
            2 => ZustandFarbe.Mittel,
            _ => ZustandFarbe.Schlecht,
        };
    }
}
