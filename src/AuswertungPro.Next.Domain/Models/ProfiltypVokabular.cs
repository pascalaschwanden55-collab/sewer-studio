using System.Collections.ObjectModel;

namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Die Profilformen, die in der Urner GEONIS-Maske angeboten werden.
///
/// Im Programm stehen lesbare deutsche Namen. Fuer die XTF liefert
/// <see cref="NachNorm"/> die zeichengenaue SIA405-Schreibweise. Die alte Auswahl
/// "Anderes" hat im Modell 2020 keinen eigenen Wert mehr. Alte Importwerte mit
/// dieser Bezeichnung werden deshalb auf <c>Spezialprofil</c> angehoben.
/// </summary>
public static class ProfiltypVokabular
{
    public static readonly IReadOnlyList<string> Auswahl = new ReadOnlyCollection<string>(
    [
        "",
        "Unbekannt",
        "Kreisprofil",
        "Eiprofil",
        "Maulprofil",
        "Offenes Profil",
        "Rechteckprofil",
        "Spezialprofil"
    ]);

    private static readonly IReadOnlyDictionary<string, (string Anzeige, string Norm)> Zuordnungen =
        new Dictionary<string, (string Anzeige, string Norm)>(StringComparer.OrdinalIgnoreCase)
        {
            ["unbekannt"] = ("Unbekannt", "unbekannt"),
            ["U"] = ("Unbekannt", "unbekannt"),
            ["Unbekannt (U)"] = ("Unbekannt", "unbekannt"),
            ["Kreisprofil"] = ("Kreisprofil", "Kreisprofil"),
            ["K"] = ("Kreisprofil", "Kreisprofil"),
            ["Kreisprofil (K)"] = ("Kreisprofil", "Kreisprofil"),
            ["Eiprofil"] = ("Eiprofil", "Eiprofil"),
            ["E"] = ("Eiprofil", "Eiprofil"),
            ["Eiprofil (E)"] = ("Eiprofil", "Eiprofil"),
            ["Maulprofil"] = ("Maulprofil", "Maulprofil"),
            ["M"] = ("Maulprofil", "Maulprofil"),
            ["Maulprofil (M)"] = ("Maulprofil", "Maulprofil"),
            ["offenes_Profil"] = ("Offenes Profil", "offenes_Profil"),
            ["Offenes Profil"] = ("Offenes Profil", "offenes_Profil"),
            ["OP"] = ("Offenes Profil", "offenes_Profil"),
            ["Offenes Profil (OP)"] = ("Offenes Profil", "offenes_Profil"),
            ["Rechteckprofil"] = ("Rechteckprofil", "Rechteckprofil"),
            ["R"] = ("Rechteckprofil", "Rechteckprofil"),
            ["Rechteckprofil (R)"] = ("Rechteckprofil", "Rechteckprofil"),
            ["Spezialprofil"] = ("Spezialprofil", "Spezialprofil"),
            ["S"] = ("Spezialprofil", "Spezialprofil"),
            ["Spezialprofil (S)"] = ("Spezialprofil", "Spezialprofil"),
            ["andere"] = ("Spezialprofil", "Spezialprofil"),
            ["Anderes"] = ("Spezialprofil", "Spezialprofil"),
            ["A"] = ("Spezialprofil", "Spezialprofil"),
            ["Anderes (A)"] = ("Spezialprofil", "Spezialprofil")
        };

    /// <summary>Lesbare Schreibweise fuer Anzeige und Projektspeicherung.</summary>
    public static string Normalisieren(string? wert)
    {
        var text = (wert ?? "").Trim();
        return Zuordnungen.TryGetValue(text, out var zuordnung)
            ? zuordnung.Anzeige
            : text;
    }

    /// <summary>Gueltiger SIA405-Wert oder <c>null</c>, wenn der Wert unbekannt ist.</summary>
    public static string? NachNorm(string? wert)
    {
        var text = (wert ?? "").Trim();
        if (text.Length == 0)
            return null;

        if (Zuordnungen.TryGetValue(text, out var zuordnung))
            return zuordnung.Norm;

        return SiaKanalVokabular.Profiltyp.NachNorm(text);
    }
}
