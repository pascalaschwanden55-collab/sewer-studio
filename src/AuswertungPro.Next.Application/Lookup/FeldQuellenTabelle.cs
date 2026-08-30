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
    Grundbuch,

    /// <summary>
    /// Abwassernetz des Kantons Uri (Netzabfrage). Eigene Quelle neben dem
    /// Kataster, weil der XTF-Export die Eigentuemer einplattet — der Dienst
    /// kennt sie noch.
    /// </summary>
    Abwassernetz
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

            // Der Eigentuemer kommt NICHT aus dem Kataster: Der QGIS-Export
            // nach XTF plattet die Zuordnung ein — dort tragen alle Leitungen
            // denselben Verweis, obwohl der Kopf der Datei 27 verschiedene
            // Eigentuemer nennt. Der Abwassernetz-Dienst des Kantons kennt sie
            // noch und liefert etwa "Privat" fuer private Hausanschluesse.
            ["Eigentuemer"] = FeldQuelle.Abwassernetz,
            ["Eigentümer"] = FeldQuelle.Abwassernetz,
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
