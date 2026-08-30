using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Lookup;

/// <summary>Woher ein nachgeschlagener Feldwert stammt.</summary>
public enum FeldQuelle
{
    /// <summary>Lokaler Abwasserkataster (XTF-Datei).</summary>
    Kataster,

    /// <summary>Grundbuchauskunft des Kantons Uri (Netzabfrage).</summary>
    Grundbuch
}

/// <summary>
/// Welches Schachtfeld aus welcher Quelle kommt. Bewusst eine Tabelle und
/// keine Verzweigung im UseCase: So bleibt die Zuordnung testbar und laesst
/// sich ohne Aenderung an der Oberflaeche erweitern.
///
/// Felder, die der Bearbeiter selbst fuellt (Kosten, Massnahmen,
/// Zustandsklasse), stehen bewusst nicht darin — dort waere ein Menuepunkt
/// eine leere Zusage.
/// </summary>
public static class FeldQuellenTabelle
{
    private static readonly Dictionary<string, FeldQuelle> Zuordnung =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Funktion"] = FeldQuelle.Kataster,
            ["Material"] = FeldQuelle.Kataster,

            // Beide Schreibweisen kommen in echten Projekten vor.
            ["Eigentuemer"] = FeldQuelle.Grundbuch,
            ["Eigentümer"] = FeldQuelle.Grundbuch,
            ["Strasse"] = FeldQuelle.Grundbuch,
        };

    public static IReadOnlyList<string> UnterstuetzteFelder => Zuordnung.Keys.ToList();

    public static FeldQuelle? QuelleFuer(string? feldname)
        => feldname is not null && Zuordnung.TryGetValue(feldname.Trim(), out var quelle)
            ? quelle
            : null;
}
