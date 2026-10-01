using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>An welchem Ende einer Haltung der Schacht liegt.</summary>
public enum Haltungsseite
{
    /// <summary>Die Haltung gehoert nicht zu diesem Schacht.</summary>
    Keine,

    /// <summary>Der Schacht ist der obere Schacht: Die Haltung verlaesst ihn (Auslauf des Schachts).</summary>
    Oben,

    /// <summary>Der Schacht ist der untere Schacht: Die Haltung muendet ein (Einlauf des Schachts).</summary>
    Unten
}

/// <summary>
/// Ordnet eine Haltung einem Schacht zu. Zuerst zaehlen die Felder <c>Schacht_oben</c> und
/// <c>Schacht_unten</c>; sind BEIDE leer, entscheidet der Haltungsname nach dem Muster
/// <c>&lt;oben&gt;-&lt;unten&gt;</c>. Der Namensweg ist noetig, weil Importe die Schachtfelder
/// nicht immer fuellen (Zone 1.15: leer bei allen 96 Haltungen, der Name traegt sie).
/// Gefuellte Felder haben Vorrang: Bei einer Gegenbefahrung kann der Name die Schaechte in
/// der anderen Reihenfolge tragen.
/// </summary>
public static class SchachtHaltungsseite
{
    public static Haltungsseite Bestimme(HaltungRecord haltung, string? schachtnummer)
    {
        ArgumentNullException.ThrowIfNull(haltung);
        var nummer = (schachtnummer ?? "").Trim();
        if (nummer.Length == 0)
            return Haltungsseite.Keine;

        var oben = (haltung.GetFieldValue("Schacht_oben") ?? "").Trim();
        var unten = (haltung.GetFieldValue("Schacht_unten") ?? "").Trim();
        if (oben.Length > 0 || unten.Length > 0)
        {
            if (string.Equals(oben, nummer, StringComparison.OrdinalIgnoreCase))
                return Haltungsseite.Oben;
            if (string.Equals(unten, nummer, StringComparison.OrdinalIgnoreCase))
                return Haltungsseite.Unten;
            return Haltungsseite.Keine;
        }

        var name = (haltung.GetFieldValue(FieldKeys.HoldingName) ?? "").Trim();
        var trenner = name.IndexOf('-');
        if (trenner <= 0 || trenner >= name.Length - 1)
            return Haltungsseite.Keine;

        if (string.Equals(name[..trenner], nummer, StringComparison.OrdinalIgnoreCase))
            return Haltungsseite.Oben;
        if (string.Equals(name[(trenner + 1)..], nummer, StringComparison.OrdinalIgnoreCase))
            return Haltungsseite.Unten;
        return Haltungsseite.Keine;
    }
}
