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
        var v = invertiert ? 5 - wert.Value : wert.Value;
        return v switch
        {
            <= 1 => ZustandFarbe.Gut,
            2 => ZustandFarbe.Mittel,
            _ => ZustandFarbe.Schlecht,
        };
    }
}
