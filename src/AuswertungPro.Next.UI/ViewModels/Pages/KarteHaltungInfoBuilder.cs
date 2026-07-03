using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>Anzeige-Daten fuer das Karten-Infopanel einer angeklickten Haltung.</summary>
public sealed record KarteHaltungInfo(
    string Name,
    string Dn,
    string Material,
    string Laenge,
    string Zustandsklasse,
    bool HatVideo);

/// <summary>
/// Baut die Infopanel-Daten aus einem HaltungRecord — leere Felder werden
/// als "—" angezeigt, damit das Panel immer vollstaendig aussieht.
/// </summary>
public static class KarteHaltungInfoBuilder
{
    public static KarteHaltungInfo? Build(HaltungRecord? record)
    {
        if (record is null)
            return null;

        var dn = Wert(record, "DN_mm");
        var laenge = Wert(record, "Haltungslaenge_m");

        return new KarteHaltungInfo(
            Name: Wert(record, "Haltungsname"),
            Dn: dn == "—" ? "—" : $"DN {dn}",
            Material: Wert(record, "Rohrmaterial"),
            Laenge: laenge == "—" ? "—" : $"{laenge} m",
            Zustandsklasse: Wert(record, "Zustandsklasse"),
            HatVideo: !string.IsNullOrWhiteSpace(record.GetFieldValue("Link")));
    }

    private static string Wert(HaltungRecord record, string feld)
    {
        var wert = (record.GetFieldValue(feld) ?? string.Empty).Trim();
        return wert.Length == 0 ? "—" : wert;
    }
}
