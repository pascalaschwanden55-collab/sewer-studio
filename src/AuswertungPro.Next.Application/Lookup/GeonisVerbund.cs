using System;

namespace AuswertungPro.Next.Application.Lookup;

/// <summary>Wie weit ein Bauteil für den GEONIS-Weg gerüstet ist.</summary>
public enum GeonisVerbundStand
{
    /// <summary>Die Zuordnung zum Kataster ist offen — mehrdeutig, fehlend oder strittig.</summary>
    Ungeklaert = 0,

    /// <summary>
    /// Ein Neu-Export ist möglich, aber mit selbst vergebenen Kennungen. GEONIS legt
    /// dabei neue Objekte an; eine Aktualisierung ist das nicht.
    /// </summary>
    NurNeuExport = 1,

    /// <summary>
    /// Der ganze Objektverbund ist bekannt — GEONIS kann die vorhandenen Objekte
    /// wiedererkennen.
    /// </summary>
    AbgleichMoeglich = 2
}

/// <summary>
/// Bewertet, ob die bekannten Kennungen für einen echten GEONIS-Abgleich reichen.
///
/// Der Unterschied ist wesentlich und wird im Bericht oft verwechselt: Ein Neu-Export
/// gelingt IMMER — notfalls mit eigenen <c>chSST</c>-Kennungen. Beim Import in einen
/// bereits gefüllten Kataster entstehen daraus aber **Duplikate**, keine Aktualisierung.
/// Erst der vollständige Objektverbund macht daraus ein Wiedererkennen.
///
/// Eine Haltung braucht dafür Haltung, Kanal und beide Haltungspunkte; ein Schacht
/// Knoten und Bauwerk. Das Rohrprofil bleibt aussen vor: Es wird in GEONIS von vielen
/// Haltungen geteilt (56 Profile für 102'317 Haltungen), und ein fehlendes Profil
/// verhindert das Wiedererkennen der Haltung nicht.
///
/// Reine Rechnung: kein Datei-, Netz- oder UI-Zugriff.
/// </summary>
public static class GeonisVerbund
{
    public static GeonisVerbundStand Bestimme(KatasterKennung? kennung, BauteilArt art)
    {
        if (kennung is null)
            return GeonisVerbundStand.Ungeklaert;

        var vollstaendig = art == BauteilArt.Haltung
            ? Gesetzt(kennung.Haltung)
              && Gesetzt(kennung.Kanal)
              && Gesetzt(kennung.VonPunkt)
              && Gesetzt(kennung.NachPunkt)
            : Gesetzt(kennung.Knoten) && Gesetzt(kennung.Bauwerk);

        return vollstaendig ? GeonisVerbundStand.AbgleichMoeglich : GeonisVerbundStand.NurNeuExport;
    }

    private static bool Gesetzt(string? wert) => !string.IsNullOrWhiteSpace(wert);
}
