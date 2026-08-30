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

            // Gemeint ist der Eigentuemer des BAUWERKS (Privat, Abwasser Uri,
            // Kanton Uri, eine Gemeinde) — nicht der Grundstuecksbesitzer aus
            // dem Grundbuch. Bei manchen Anlagen gehoert das Bauwerk nicht
            // dem, dem das Land gehoert; im Eigentuemerdossier geht es dagegen
            // um den Besitzer der Liegenschaft.
            //
            // Die XTF taugt dafuer nicht: Dort tragen alle Bauwerke denselben
            // Verweis. Beide Schreibweisen kommen in echten Projekten vor.
            ["Eigentuemer"] = FeldQuelle.Abwassernetz,
            ["Eigentümer"] = FeldQuelle.Abwassernetz,

            // Die Gebaeudeadresse kennt nur das Grundbuch.
            ["Strasse"] = FeldQuelle.Grundbuch,
        };

    private static readonly Dictionary<string, FeldQuelle> Haltung =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Material und Laenge stehen auch im lokalen Kataster. Sie kommen
            // trotzdem aus dem Netzdienst: Er liefert in derselben Abfrage die
            // uebrigen Felder mit, und seine Angaben sind aktueller (SDE-Stand
            // statt Exportdatum).
            ["Rohrmaterial"] = FeldQuelle.Abwassernetz,
            ["Haltungslaenge_m"] = FeldQuelle.Abwassernetz,

            // In 473 von 475 Haltungen leer - und in der XTF gar nicht
            // vorhanden. Nur der Netzdienst kennt dieses Feld.
            ["FunktionHierarchisch"] = FeldQuelle.Abwassernetz,

            // In 113 von 475 Haltungen leer.
            ["Nutzungsart"] = FeldQuelle.Abwassernetz,
            ["Nutzungsart_Ist"] = FeldQuelle.Abwassernetz,

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
