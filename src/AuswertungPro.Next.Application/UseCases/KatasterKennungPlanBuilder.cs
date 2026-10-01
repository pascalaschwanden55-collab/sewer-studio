using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases;

/// <summary>
/// Ein Bauteil, das seine GEONIS-Kennungen bekaeme. <see cref="NurAnzeige"/>: Es traegt
/// sie schon, nur das sichtbare Anzeigefeld ist leer und wird nachgezogen.
/// </summary>
public sealed record KatasterKennungPosition(
    string Bauteil, KatasterKennung Kennung, bool Gedreht, bool NurAnzeige = false);

/// <summary>Warum ein Bauteil nichts bekommt.</summary>
public enum KatasterKennungGrund
{
    /// <summary>Der Name kommt in der Kennungstabelle mehrfach vor.</summary>
    Mehrdeutig,

    /// <summary>Der Name kommt in der Kennungstabelle nicht vor.</summary>
    NichtGefunden,

    /// <summary>Das Bauteil traegt dieselbe Kennung schon.</summary>
    BereitsVorhanden,

    /// <summary>Das Bauteil traegt eine ANDERE bestaetigte Kennung — die bleibt stehen.</summary>
    Abweichend,

    /// <summary>
    /// Das Bauteil traegt eine andere Kennung unbekannter Herkunft. Sie wird nicht
    /// ueberschrieben, der Fall gehoert aber geprueft.
    /// </summary>
    HerkunftUnklar
}

public sealed record KatasterKennungHinweis(string Bauteil, KatasterKennungGrund Grund);

/// <summary>
/// Was die Uebernahme tun wuerde — vollstaendig, bevor irgendetwas geschrieben wird.
/// </summary>
public sealed record KatasterKennungPlan(
    BauteilArt Art,
    IReadOnlyList<KatasterKennungPosition> Positionen,
    IReadOnlyList<KatasterKennungHinweis> Hinweise,
    int GepruefteBauteile,
    string Stand)
{
    public bool OhneAenderung => Positionen.Count == 0;

    public int Gedreht => Positionen.Count(p => p.Gedreht);

    /// <summary>Bauteile, die nur ihr Anzeigefeld nachgezogen bekommen.</summary>
    public int NurAnzeige => Positionen.Count(p => p.NurAnzeige);

    /// <summary>Bauteile, die wirklich neue Kennungen bekommen.</summary>
    public int Neu => Positionen.Count - NurAnzeige;

    public int Anzahl(KatasterKennungGrund grund) => Hinweise.Count(h => h.Grund == grund);

    /// <summary>
    /// Bauteile, deren ganzer Objektverbund bekannt ist — nur bei ihnen kann GEONIS die
    /// vorhandenen Objekte wiedererkennen.
    /// </summary>
    public int AbgleichMoeglich => Positionen.Count(p =>
        GeonisVerbund.Bestimme(p.Kennung, Art) == GeonisVerbundStand.AbgleichMoeglich);

    /// <summary>
    /// Bauteile mit unvollstaendigem Verbund. Ein Neu-Export gelingt, legt in einem
    /// gefuellten Kataster aber neue Objekte an statt die vorhandenen zu aendern.
    /// </summary>
    public int NurNeuExport => Positionen.Count - AbgleichMoeglich;

    /// <summary>Bauteile, deren Zuordnung offen ist und die von Hand geklaert werden muessen.</summary>
    public int Ungeklaert =>
        Anzahl(KatasterKennungGrund.Mehrdeutig)
        + Anzahl(KatasterKennungGrund.Abweichend)
        + Anzahl(KatasterKennungGrund.HerkunftUnklar);
}

/// <summary>
/// Plant, welche Bauteile ihre GEONIS-Kennungen aus der Kennungstabelle bekaemen.
///
/// Drei Regeln:
///
/// 1. <b>Nur bei genau einem Treffer.</b> Ein mehrdeutiger Name liefert nichts.
///    Direkter Treffer zuerst; bei Haltungen danach die Gegenrichtung ("B-A" fuer
///    "A-B"), weil das Projekt bei einer Gegenbefahrung den unteren Schacht vorn
///    fuehrt. Schaechte kennen keine Richtung.
/// 2. <b>Eine vorhandene Kennung wird nie ueberschrieben.</b> Traegt das Bauteil
///    schon eine andere GEONIS-Kennung, bleibt sie stehen und der Bericht sagt es.
///    Sie kann aus einem GEONIS-Export stammen, der aktueller ist als die Tabelle.
/// 3. <b>Nur Kennungen, keine Fachwerte.</b> Material, Zustand oder Masse aus
///    der Tabelle werden nicht einmal gelesen. Die Kopie ist alt; ihre Werte
///    wuerden den Projektstand nur verfaelschen.
///
/// Reine Rechnung ohne Dateizugriff und ohne Mutation.
/// </summary>
public static class KatasterKennungPlanBuilder
{
    public static KatasterKennungPlan BaueFuerHaltungen(
        IEnumerable<HaltungRecord> haltungen, KatasterKennungBestand bestand)
    {
        ArgumentNullException.ThrowIfNull(haltungen);

        return Baue(
            BauteilArt.Haltung,
            haltungen.Select(h =>
            {
                var (quelle, handgesetzt) = LiesHerkunft(h.FieldMeta, FieldKeys.CadastreObjectId);
                return new Bauteilsicht(
                    h.GetFieldValue(FieldKeys.HoldingName),
                    h.Geonis,
                    string.IsNullOrWhiteSpace(h.GetFieldValue(FieldKeys.GeonisId)),
                    h.GetFieldValue(FieldKeys.CadastreObjectId))
                {
                    ObjektIdQuelle = quelle,
                    ObjektIdHandgesetzt = handgesetzt
                };
            }),
            bestand,
            mitGegenrichtung: true);
    }

    public static KatasterKennungPlan BaueFuerSchaechte(
        IEnumerable<SchachtRecord> schaechte, KatasterKennungBestand bestand)
    {
        ArgumentNullException.ThrowIfNull(schaechte);

        return Baue(
            BauteilArt.Schacht,
            schaechte.Select(s =>
            {
                var feld = SchachtFeldnamen.Feld(s, FieldKeys.CadastreObjectId);
                var (quelle, handgesetzt) = LiesHerkunft(s.FieldMeta, feld);
                return new Bauteilsicht(
                    s.GetFieldValue(SchachtFeldnamen.Feld(s, "Schachtnummer")),
                    s.Geonis,
                    string.IsNullOrWhiteSpace(s.GetFieldValue(SchachtFeldnamen.Feld(s, FieldKeys.GeonisId))),
                    s.GetFieldValue(feld))
                {
                    ObjektIdQuelle = quelle,
                    ObjektIdHandgesetzt = handgesetzt
                };
            }),
            bestand,
            mitGegenrichtung: false);
    }

    /// <summary>
    /// Der Name in Gegenrichtung, oder <c>null</c>, wenn er keine hat: Nur ein Name
    /// aus genau zwei Teilen um einen Bindestrich laesst sich drehen.
    /// </summary>
    public static string? Gegenrichtung(string? name)
    {
        var text = (name ?? "").Trim();
        var teile = text.Split('-');
        if (teile.Length != 2)
            return null;

        var links = teile[0].Trim();
        var rechts = teile[1].Trim();
        if (links.Length == 0 || rechts.Length == 0)
            return null;

        return $"{rechts}-{links}";
    }

    private sealed record Bauteilsicht(
        string? Name, GeonisKennungen? Vorhanden, bool AnzeigeLeer, string? ObjektId)
    {
        /// <summary>Herkunft des Werts in <see cref="ObjektId"/>.</summary>
        public FieldSource ObjektIdQuelle { get; init; } = FieldSource.Unknown;

        /// <summary>Wurde <see cref="ObjektId"/> von Hand gesetzt oder bestaetigt?</summary>
        public bool ObjektIdHandgesetzt { get; init; }
    }

    private static (FieldSource Quelle, bool Handgesetzt) LiesHerkunft(
        IReadOnlyDictionary<string, FieldMetadata> feldmetadaten, string feld)
        => feldmetadaten.TryGetValue(feld, out var meta)
            ? (meta.Source, meta.UserEdited)
            : (FieldSource.Unknown, false);

    private static KatasterKennungPlan Baue(
        BauteilArt art, IEnumerable<Bauteilsicht> bauteile, KatasterKennungBestand bestand,
        bool mitGegenrichtung)
    {
        ArgumentNullException.ThrowIfNull(bestand);

        var positionen = new List<KatasterKennungPosition>();
        var hinweise = new List<KatasterKennungHinweis>();
        var geprueft = 0;

        foreach (var bauteil in bauteile)
        {
            var name = (bauteil.Name ?? "").Trim();
            if (name.Length == 0)
                continue;

            geprueft++;

            var (kennung, gedreht, grund) = Suche(name, bestand, mitGegenrichtung);
            if (kennung is null)
            {
                hinweise.Add(new KatasterKennungHinweis(name, grund));
                continue;
            }

            var vorhanden = art == BauteilArt.Haltung ? bauteil.Vorhanden?.Haltung : bauteil.Vorhanden?.Knoten;
            if (!string.IsNullOrWhiteSpace(vorhanden))
            {
                var gleich = string.Equals(vorhanden.Trim(), kennung.Hauptkennung, StringComparison.Ordinal);

                // Die Kennung ist schon da, nur das sichtbare Feld leer (Bestand von vor
                // dem Anzeigefeld): nachziehen, ohne die Kennungen selbst anzufassen.
                if (gleich && bauteil.AnzeigeLeer)
                {
                    positionen.Add(new KatasterKennungPosition(name, kennung, gedreht, NurAnzeige: true));
                    continue;
                }

                hinweise.Add(new KatasterKennungHinweis(
                    name, gleich ? KatasterKennungGrund.BereitsVorhanden : KatasterKennungGrund.Abweichend));
                continue;
            }

            // Nur eine nachweislich lokale Exportkennung ist ersetzbar. Eine
            // importierte XTF-TID kann eine echte GEONIS-Kennung sein; allein die
            // Feldherkunft erlaubt deshalb keinen Austausch gegen eine andere ID.
            var importiert = (bauteil.ObjektId ?? "").Trim();
            if (!string.Equals(importiert, kennung.Hauptkennung, StringComparison.Ordinal))
            {
                var herkunft = KatasterKennungHerkunft.Bestimme(
                    importiert, bauteil.ObjektIdQuelle, bauteil.ObjektIdHandgesetzt);

                if (KatasterKennungHerkunft.BlockiertUebernahme(herkunft))
                {
                    hinweise.Add(new KatasterKennungHinweis(
                        name,
                        herkunft == KennungsHerkunft.BestaetigtesGeonis
                            ? KatasterKennungGrund.Abweichend
                            : KatasterKennungGrund.HerkunftUnklar));
                    continue;
                }
            }

            positionen.Add(new KatasterKennungPosition(name, kennung, gedreht));
        }

        return new KatasterKennungPlan(art, positionen, hinweise, geprueft, bestand.Stand);
    }

    private static (KatasterKennung? Kennung, bool Gedreht, KatasterKennungGrund Grund) Suche(
        string name, KatasterKennungBestand bestand, bool mitGegenrichtung)
    {
        if (bestand.IstMehrdeutig(name))
            return (null, false, KatasterKennungGrund.Mehrdeutig);

        var direkt = bestand.Finde(name);
        if (direkt is not null)
            return (direkt, false, default);

        if (!mitGegenrichtung)
            return (null, false, KatasterKennungGrund.NichtGefunden);

        var gedreht = Gegenrichtung(name);
        if (gedreht is null)
            return (null, false, KatasterKennungGrund.NichtGefunden);

        if (bestand.IstMehrdeutig(gedreht))
            return (null, false, KatasterKennungGrund.Mehrdeutig);

        var treffer = bestand.Finde(gedreht);
        return treffer is null
            ? (null, false, KatasterKennungGrund.NichtGefunden)
            : (treffer, true, default);
    }
}
