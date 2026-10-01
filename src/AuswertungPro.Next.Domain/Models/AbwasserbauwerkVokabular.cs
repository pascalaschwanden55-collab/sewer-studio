namespace AuswertungPro.Next.Domain.Models;

/// <summary>Bauwerksklasse getrennt von der Funktion; unbekannte Typen werden nicht geraten.</summary>
public static class AbwasserbauwerkVokabular
{
    public static IReadOnlyList<string> Auswahl { get; } = Array.AsReadOnly(new[]
        { "", "Normschacht", "Spezialbauwerk", "Versickerungsanlage", "Einleitstelle" });

    public static IReadOnlyList<string> Versickerungsarten { get; } = Array.AsReadOnly(new[]
    {
        "", "andere_mit_Bodenpassage", "andere_ohne_Bodenpassage", "Flaechenfoermige_Versickerung",
        "Kieskoerper", "Kombination_Schacht_Strang", "MuldenRigolenversickerung", "unbekannt",
        "Versickerung_ueber_die_Schulter", "Versickerungsbecken", "Versickerungsschacht",
        "Versickerungsstrang_Galerie"
    });

    public static IReadOnlyList<string> Spezialfunktionen { get; } = Array.AsReadOnly(new[]
    {
        "abflussloseGrube", "Absturzbauwerk", "Abwasserfaulraum", "andere", "Be_Entlueftung",
        "Behandlungsanlage", "Duekerkammer", "Duekeroberhaupt", "Faulgrube", "Fettabscheider",
        "Gelaendemulde", "Geschiebefang", "Guellegrube", "Havariebecken", "Klaergrube",
        "Kombischacht", "Kontroll_Einsteigschacht", "Oelabscheider", "Pumpwerk",
        "Regenbecken_Durchlaufbecken", "Regenbecken_Fangbecken", "Regenbecken_Fangkanal",
        "Regenbecken_Regenklaerbecken", "Regenbecken_Regenrueckhaltebecken",
        "Regenbecken_Regenrueckhaltekanal", "Regenbecken_Stauraumkanal", "Regenbecken_Verbundbecken",
        "Regenueberlauf", "Schwimmstoffabscheider", "seitlicherZugang", "Spuelschacht",
        "Trennbauwerk", "unbekannt", "Vorbehandlungsanlage", "Wirbelfallschacht"
    });

    public static string? Klasse(string? bauwerksart, string? funktion)
    {
        var explizit = (bauwerksart ?? "").Trim();
        if (explizit.Length > 0)
            return Auswahl.FirstOrDefault(a => a.Equals(explizit, StringComparison.OrdinalIgnoreCase));
        return (funktion ?? "").Trim().ToLowerInvariant() switch
        {
            "sickerschacht" or "versickerungsschacht" or "versickerungsanlage" => "Versickerungsanlage",
            "spezialbauwerk" => "Spezialbauwerk",
            "einleitstelle" => "Einleitstelle",
            _ => "Normschacht"
        };
    }

    public static string? Spezialfunktion(string roh)
    {
        var direkt = Spezialfunktionen.FirstOrDefault(v => v.Equals(roh.Trim(), StringComparison.OrdinalIgnoreCase));
        if (direkt is not null) return direkt;
        var norm = SchachtFunktionVokabular.NachNorm(roh);
        return norm is not null && Spezialfunktionen.Contains(norm) ? norm : null;
    }
}
