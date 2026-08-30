using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Lookup;

/// <summary>Woher ein nachgeschlagener Feldwert stammt.</summary>
public enum FeldQuelle
{
    /// <summary>Lokaler Abwasserkataster (XTF-Datei), ohne Netzzugriff.</summary>
    Kataster,

    /// <summary>Grundbuchauskunft des Kantons Uri (Netzabfrage).</summary>
    Grundbuch
}

/// <summary>
/// Welches Feld aus welcher Quelle kommt — getrennt nach Schacht und Haltung.
/// Bewusst eine Tabelle und keine Verzweigung im UseCase: So bleibt die
/// Zuordnung testbar und laesst sich ohne Aenderung an der Oberflaeche
/// erweitern.
///
/// "Eigentuemer" ist der Grund, warum die Bauteilart mitgegeben werden muss:
/// Bei einem Schacht ist der Grundstueckseigentuemer gemeint (Grundbuch), bei
/// einer Haltung der Netzbetreiber (Kataster).
///
/// Felder, die der Bearbeiter selbst fuellt (Kosten, Massnahmen,
/// Zustandsklasse), stehen bewusst nicht darin — dort waere ein Menuepunkt
/// eine leere Zusage.
/// </summary>
public static class FeldQuellenTabelle
{
    private static readonly Dictionary<string, FeldQuelle> Schacht =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Funktion"] = FeldQuelle.Kataster,
            ["Material"] = FeldQuelle.Kataster,

            // Beide Schreibweisen kommen in echten Projekten vor.
            ["Eigentuemer"] = FeldQuelle.Grundbuch,
            ["Eigentümer"] = FeldQuelle.Grundbuch,
            ["Strasse"] = FeldQuelle.Grundbuch,
        };

    private static readonly Dictionary<string, FeldQuelle> Haltung =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Rohrmaterial"] = FeldQuelle.Kataster,
            ["Haltungslaenge_m"] = FeldQuelle.Kataster,

            // Beim Netz ist der Betreiber gemeint, nicht der Grundeigentuemer.
            ["Eigentuemer"] = FeldQuelle.Kataster,
            ["Eigentümer"] = FeldQuelle.Kataster,
        };

    public static IReadOnlyList<string> UnterstuetzteFelder(BauteilArt art)
        => Tabelle(art).Keys.ToList();

    public static FeldQuelle? QuelleFuer(string? feldname, BauteilArt art = BauteilArt.Schacht)
        => feldname is not null && Tabelle(art).TryGetValue(feldname.Trim(), out var quelle)
            ? quelle
            : null;

    private static Dictionary<string, FeldQuelle> Tabelle(BauteilArt art)
        => art == BauteilArt.Haltung ? Haltung : Schacht;
}
