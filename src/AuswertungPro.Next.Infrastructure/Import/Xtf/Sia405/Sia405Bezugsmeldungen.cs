using AuswertungPro.Next.Infrastructure.Import.Common;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf.Sia405;

/// <summary>
/// Zwischen Schritt 2 (Bezuege) und Schritt 3 (Abbildung) des SIA405-Haltungsimports:
/// meldet, was sich nicht aufloesen liess. Bis 01.10.2026 fiel das still weg (Befund 2
/// aus AP08). Hier wird nur gemeldet, kein Wert geaendert; der Rueckfall eines
/// Profilverweises ins Leere auf <c>Lichte_Breite</c> bleibt.
///
/// Organisationen sind in SIA405/DSS EXTERNAL: Ein Verweis auf eine Kennung, die die Datei
/// gar nicht enthaelt, ist erlaubt (GeoShop liefert z.B. keinen Datenherrn mit). Er ist
/// darum kein Fehler, sondern ein gesammelter Hinweis — dieselbe Trennung wie
/// <c>DssExportPruefung</c> und <c>XtfLieferungsNorm.ExterneOrganisation</c>. Eine Warnung
/// gibt es nur, wenn die Organisation in der Datei steht, aber keine Bezeichnung traegt.
/// </summary>
internal static class Sia405Bezugsmeldungen
{
    private const string Kontext = "XTF405";

    /// <param name="haltungen">Die Haltungen, die uebernommen werden (nach Dopplungsregel).</param>
    /// <param name="ohneNamen">Die weggefallenen Haltungen ohne Namen.</param>
    /// <param name="verweise">
    /// Die Organisationsverweise der Datei, in die schon die Schaechte gezaehlt sind; der
    /// Hinweis zu externen Organisationen steht so je Datei einmal. Ohne: nur die Haltungen.
    /// </param>
    public static List<ImportMessage> Erzeuge(
        Sia405Bestand bestand,
        IReadOnlyList<Sia405HaltungMitBezuegen> haltungen,
        IReadOnlyList<Sia405HaltungObjekt> ohneNamen,
        Sia405Organisationsverweise? verweise = null)
    {
        ArgumentNullException.ThrowIfNull(bestand);
        ArgumentNullException.ThrowIfNull(haltungen);
        ArgumentNullException.ThrowIfNull(ohneNamen);

        var meldungen = new List<ImportMessage>();
        verweise ??= new Sia405Organisationsverweise(bestand.OrganisationenInDatei);

        foreach (var h in haltungen)
        {
            var kopf = $"Haltung \"{h.Haltungsname}\" (TID {h.Haltung.Tid}): ";
            var hd = h.Haltung;

            if (h.KanalBezug == Sia405KanalBezug.Fehlt && Gesetzt(hd.KanalRef))
                Warnung(meldungen, kopf + $"Kanalverweis {hd.KanalRef} zeigt ins Leere – Kanalangaben "
                                        + "(Eigentümer, Status, Nutzungsart u. a.) nicht übernommen.");

            if (h.RohrprofilVerweisOhneZiel)
                Warnung(meldungen, kopf + $"Rohrprofilverweis {hd.RohrprofilRef} zeigt ins Leere – "
                                        + (Gesetzt(hd.LichteHoehe) && Gesetzt(hd.LichteBreite)
                                            ? "Profiltyp fehlt, Breite aus Lichte_Breite der Haltung übernommen."
                                            : "Profiltyp und Breite fehlen."));

            PruefePunkt(meldungen, bestand, kopf, hd.VonRef, "oben", h.SchachtOben);
            PruefePunkt(meldungen, bestand, kopf, hd.NachRef, "unten", h.SchachtUnten);

            // Die Organisationen haengen am Kanal. Ein nicht leerer Eigentuemertext hat
            // Vorrang vor dem Verweis; dann fehlt nichts.
            if (h.Kanal is { } kanal)
            {
                if (!Gesetzt(kanal.Eigentuemer))
                    WarnungFalls(meldungen, verweise.Pruefe(kopf, "Eigentümer", kanal.EigentuemerRef, h.EigentuemerAusVerweis is not null, schacht: false));
                WarnungFalls(meldungen, verweise.Pruefe(kopf, "Datenherr", kanal.DatenherrRef, h.Datenherr is not null, schacht: false));
                WarnungFalls(meldungen, verweise.Pruefe(kopf, "Datenlieferant", kanal.DatenlieferantRef, h.Datenlieferant is not null, schacht: false));
            }
        }

        foreach (var hd in ohneNamen)
        {
            var kanal = Gesetzt(hd.KanalRef) ? $" (Kanalverweis {hd.KanalRef})" : "";
            Warnung(meldungen, $"Haltung ohne Bezeichnung (TID {hd.Tid}) nicht übernommen: "
                               + $"Weder die Haltung noch ihr Kanal{kanal} tragen einen Namen.");
        }

        // Ohne TID liest der Leser die Haltung nicht ein (seit 01.10.2026 gemeldet).
        foreach (var hd in bestand.HaltungenOhneTid)
        {
            var name = Gesetzt(hd.Bezeichnung) ? $"Haltung \"{hd.Bezeichnung.Trim()}\"" : "Haltung ohne Bezeichnung und";
            Warnung(meldungen, $"{name} ohne TID nicht übernommen: Ohne Objektkennung lässt sie sich nicht eindeutig zuordnen.");
        }

        if (verweise.Hinweis(Kontext) is { } hinweis)
            meldungen.Add(hinweis);

        return meldungen;
    }

    private static void PruefePunkt(List<ImportMessage> meldungen, Sia405Bestand bestand, string kopf, string kennung, string ende,
        string? schacht)
    {
        if (!Gesetzt(kennung))
            return;

        if (!bestand.Haltungspunkte.TryGetValue(kennung, out var punkt))
        {
            Warnung(meldungen, kopf + $"Haltungspunktverweis {kennung} ({ende}) zeigt ins Leere – Schacht {ende} nicht übernommen.");
            return;
        }

        // Seit 01.10.2026 (zweite Runde): Der Punkt ist da, sein Knotenverweis aber zeigt ins
        // Leere. Der Schachtname faellt wie bisher auf Punkt- oder Haltungsnamen zurueck; neu
        // ist nur die Meldung. Ein Verweis auf eine Haltung der Datei (Anschluss an eine
        // Leitung) ist kein Leerverweis.
        var knoten = punkt.AbwassernetzelementRef;
        if (!Gesetzt(knoten) || bestand.Abwasserknoten.ContainsKey(knoten!) || bestand.Haltungen.ContainsKey(knoten!))
            return;

        var rueckfall = schacht is null
            ? $"Schacht {ende} nicht übernommen."
            : string.Equals(schacht.Trim(), (punkt.Bezeichnung ?? "").Trim(), StringComparison.Ordinal)
                ? $"Schacht {ende} ersatzweise aus dem Punktnamen übernommen (\"{schacht.Trim()}\")."
                : $"Schacht {ende} ersatzweise aus dem Haltungsnamen übernommen (\"{schacht.Trim()}\").";
        Warnung(meldungen, kopf + $"Abwasserknotenverweis {knoten} am Haltungspunkt {kennung} ({ende}) zeigt ins Leere – {rueckfall}");
    }

    private static void Warnung(List<ImportMessage> meldungen, string text)
        => meldungen.Add(new ImportMessage { Level = "Warn", Context = Kontext, Message = text });

    /// <summary>Eine Warnung zu einer Haltung oder einem Schacht (Kontext XTF405), sofern es eine gibt.</summary>
    internal static void WarnungFalls(List<ImportMessage> meldungen, string? text)
    {
        if (text is not null)
            Warnung(meldungen, text);
    }

    private static bool Gesetzt(string? wert) => !string.IsNullOrWhiteSpace(wert);
}
