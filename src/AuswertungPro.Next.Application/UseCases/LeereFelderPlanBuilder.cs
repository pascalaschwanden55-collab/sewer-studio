using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases;

/// <summary>Ein Feld, das aus dem QGIS-Bestand gefuellt wuerde.</summary>
public sealed record LeereFeldPosition(string Bauteil, string Feld, string Wert);

/// <summary>Warum ein Bauteil nichts bekommen hat.</summary>
public enum LeerfeldGrund
{
    /// <summary>Der Name kommt im QGIS-Bestand mehrfach vor.</summary>
    Mehrdeutig,

    /// <summary>Der Name kommt im QGIS-Bestand nicht vor.</summary>
    NichtGefunden,

    /// <summary>Gefunden, aber alle Felder sind entweder gefuellt oder ohne Angabe.</summary>
    NichtsZuErgaenzen
}

public sealed record LeerfeldHinweis(string Bauteil, LeerfeldGrund Grund);

/// <summary>
/// Was das Nachfuellen tun wuerde — vollstaendig, bevor irgendetwas geschrieben wird.
///
/// Gleiches Muster wie beim XTF-Export: Erst entsteht genau ein Plan, danach wendet
/// der Ausfuehrer ausschliesslich diesen an.
/// </summary>
public sealed record LeereFelderPlan(
    BauteilArt Art,
    IReadOnlyList<LeereFeldPosition> Positionen,
    IReadOnlyList<LeerfeldHinweis> Hinweise,
    int GepruefteBauteile)
{
    public int BetroffeneBauteile
        => Positionen.Select(p => p.Bauteil).Distinct(StringComparer.OrdinalIgnoreCase).Count();

    public bool OhneAenderung => Positionen.Count == 0;

    /// <summary>Wie oft ein bestimmtes Feld gefuellt wuerde — fuer den Bericht.</summary>
    public IReadOnlyList<KeyValuePair<string, int>> JeFeld
        => Positionen
            .GroupBy(p => p.Feld, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new KeyValuePair<string, int>(g.Key, g.Count()))
            .ToList();

    public int Anzahl(LeerfeldGrund grund) => Hinweise.Count(h => h.Grund == grund);
}

/// <summary>
/// Plant, welche LEEREN Felder aus dem QGIS-Bestand gefuellt wuerden.
///
/// Die eine Regel, an der alles haengt: <b>Ein Feld mit Inhalt wird nie angefasst.</b>
/// Egal woher der Inhalt stammt und egal, was der Bestand sagt — die Arbeit des
/// Bearbeiters gewinnt immer. Der Bestand fuellt nur Luecken.
///
/// Ein mehrdeutiger Name liefert nichts: Im Abwassernetz des Kantons tragen 2574
/// Haltungsnamen mehr als ein Objekt. Einen davon zu nehmen waere geraten und
/// saehe wie eine Tatsache aus.
///
/// Reine Rechnung ohne Dateizugriff und ohne Mutation.
/// </summary>
public static class LeereFelderPlanBuilder
{
    public static LeereFelderPlan BaueFuerHaltungen(
        IEnumerable<HaltungRecord> haltungen, QgisBestand bestand)
    {
        ArgumentNullException.ThrowIfNull(haltungen);

        return Baue(
            BauteilArt.Haltung,
            haltungen.Select(h => new Bauteilsicht(
                h.GetFieldValue(FieldKeys.HoldingName),
                feld => h.GetFieldValue(feld))),
            bestand);
    }

    public static LeereFelderPlan BaueFuerSchaechte(
        IEnumerable<SchachtRecord> schaechte, QgisBestand bestand)
    {
        ArgumentNullException.ThrowIfNull(schaechte);

        // Der Feldname kommt am Schacht aus der Excel-Kopfzeile, nicht aus einem
        // Katalog: Der Eigentuemer steht dort als "Eigentümer" mit Umlaut, waehrend
        // Import und Nachfuellen "Eigentuemer" meinen. Ohne die Aufloesung sieht der
        // Planer ein leeres Feld, wo laengst ein Wert steht.
        return Baue(
            BauteilArt.Schacht,
            schaechte.Select(s => new Bauteilsicht(
                s.GetFieldValue(SchachtFeldnamen.Feld(s, "Schachtnummer")),
                feld => s.GetFieldValue(SchachtFeldnamen.Feld(s, feld)))),
            bestand);
    }

    /// <summary>Ein Datensatz, soweit die Planung ihn braucht: Name und Feldzugriff.</summary>
    private sealed record Bauteilsicht(string? Name, Func<string, string?> Wert);

    private static LeereFelderPlan Baue(
        BauteilArt art, IEnumerable<Bauteilsicht> bauteile, QgisBestand bestand)
    {
        ArgumentNullException.ThrowIfNull(bestand);

        var positionen = new List<LeereFeldPosition>();
        var hinweise = new List<LeerfeldHinweis>();
        var felder = QgisFeldKarte.Felder(art);
        var geprueft = 0;

        foreach (var bauteil in bauteile)
        {
            var name = (bauteil.Name ?? "").Trim();
            if (name.Length == 0)
                continue;

            geprueft++;

            if (bestand.IstMehrdeutig(name))
            {
                hinweise.Add(new LeerfeldHinweis(name, LeerfeldGrund.Mehrdeutig));
                continue;
            }

            var quelle = bestand.Finde(name);
            if (quelle is null)
            {
                hinweise.Add(new LeerfeldHinweis(name, LeerfeldGrund.NichtGefunden));
                continue;
            }

            var vorher = positionen.Count;
            foreach (var feld in felder)
            {
                // Die Kernregel: Ein Feld mit Inhalt bleibt unberuehrt.
                if (!string.IsNullOrWhiteSpace(bauteil.Wert(feld)))
                    continue;

                var wert = QgisFeldKarte.Wert(quelle, feld, art);
                if (!string.IsNullOrWhiteSpace(wert))
                    positionen.Add(new LeereFeldPosition(name, feld, wert!));
            }

            if (positionen.Count == vorher)
                hinweise.Add(new LeerfeldHinweis(name, LeerfeldGrund.NichtsZuErgaenzen));
        }

        return new LeereFelderPlan(art, positionen, hinweise, geprueft);
    }
}
