using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases;

/// <summary>
/// Schreibt einen <see cref="KatasterKennungPlan"/> in die Datensaetze — und sonst nichts.
///
/// Der Ausfuehrer entscheidet nichts mehr. Er prueft nur noch einmal, dass das
/// Bauteil inzwischen keine Kennung bekommen hat; zwischen Planung und Bestaetigung
/// kann ein anderer Weg (XTF-Import) eine gesetzt haben, und die gewinnt.
/// </summary>
public static class KatasterKennungAnwender
{
    public static int WendeAnAufHaltungen(IEnumerable<HaltungRecord> haltungen, KatasterKennungPlan plan)
    {
        ArgumentNullException.ThrowIfNull(haltungen);
        ArgumentNullException.ThrowIfNull(plan);

        var jeBauteil = Gruppiere(plan);
        var jetzt = DateTime.UtcNow;
        var geschrieben = 0;

        foreach (var record in haltungen)
        {
            var name = (record.GetFieldValue(FieldKeys.HoldingName) ?? "").Trim();
            if (name.Length == 0 || !jeBauteil.TryGetValue(name, out var position))
                continue;

            if (!string.IsNullOrWhiteSpace(record.Geonis?.Haltung))
            {
                // Nur das Anzeigefeld nachziehen — die Kennungen bleiben, wie sie sind.
                if (position.NurAnzeige
                    && string.IsNullOrWhiteSpace(record.GetFieldValue(FieldKeys.GeonisId)))
                {
                    record.SetFieldValue(FieldKeys.GeonisId, record.Geonis!.Haltung, Herkunft, userEdited: false);
                    geschrieben++;
                }

                continue;
            }

            if (position.NurAnzeige)
                continue;

            var kennungen = FuerHaltung(position, plan.Stand, jetzt);
            record.SetzeGeonisKennungen(kennungen);
            record.SetFieldValue(FieldKeys.GeonisId, kennungen.Haltung, Herkunft, userEdited: false);
            geschrieben++;
        }

        return geschrieben;
    }

    /// <summary>
    /// Die Herkunft des Anzeigefelds. <c>userEdited</c> bleibt <b>false</b>: Die Kennung
    /// ist keine Handeingabe und geht nie als solche in die revidierte XTF.
    /// </summary>
    public const FieldSource Herkunft = FieldSource.Kataster;

    public static int WendeAnAufSchaechte(IEnumerable<SchachtRecord> schaechte, KatasterKennungPlan plan)
    {
        ArgumentNullException.ThrowIfNull(schaechte);
        ArgumentNullException.ThrowIfNull(plan);

        var jeBauteil = Gruppiere(plan);
        var jetzt = DateTime.UtcNow;
        var geschrieben = 0;

        foreach (var record in schaechte)
        {
            var name = (record.GetFieldValue(SchachtFeldnamen.Feld(record, "Schachtnummer")) ?? "").Trim();
            if (name.Length == 0 || !jeBauteil.TryGetValue(name, out var position))
                continue;

            var anzeigefeld = SchachtFeldnamen.Feld(record, FieldKeys.GeonisId);
            if (!string.IsNullOrWhiteSpace(record.Geonis?.Knoten))
            {
                if (position.NurAnzeige && string.IsNullOrWhiteSpace(record.GetFieldValue(anzeigefeld)))
                {
                    record.SetFieldValue(anzeigefeld, record.Geonis!.Knoten, Herkunft, userEdited: false);
                    geschrieben++;
                }

                continue;
            }

            if (position.NurAnzeige)
                continue;

            record.SetzeGeonisKennungen(new GeonisKennungen
            {
                Knoten = position.Kennung.Knoten,
                Bauwerk = position.Kennung.Bauwerk,
                Quelle = plan.Stand,
                GeonisGeaendert = position.Kennung.GeonisGeaendert,
                UebernommenUtc = jetzt
            });
            record.SetFieldValue(anzeigefeld, position.Kennung.Knoten, Herkunft, userEdited: false);
            geschrieben++;
        }

        return geschrieben;
    }

    /// <summary>
    /// Die Kennungen einer Haltung. Heisst die Haltung im Projekt in der Gegenrichtung,
    /// werden die zwei Punktkennungen vertauscht: Der Punkt am oberen Schacht des
    /// Projekts ist im Kataster der Endpunkt. So bleibt jede Kennung an ihrem Schacht.
    /// </summary>
    private static GeonisKennungen FuerHaltung(KatasterKennungPosition position, string stand, DateTime jetzt)
    {
        var k = position.Kennung;
        return new GeonisKennungen
        {
            Haltung = k.Haltung,
            Kanal = k.Kanal,
            VonPunkt = position.Gedreht ? k.NachPunkt : k.VonPunkt,
            VonPunktBezeichnung = position.Gedreht ? k.NachPunktBezeichnung : k.VonPunktBezeichnung,
            NachPunkt = position.Gedreht ? k.VonPunkt : k.NachPunkt,
            NachPunktBezeichnung = position.Gedreht ? k.VonPunktBezeichnung : k.NachPunktBezeichnung,
            Rohrprofil = k.Rohrprofil,
            RohrprofilTyp = k.RohrprofilTyp,
            RichtungGedreht = position.Gedreht,
            Quelle = stand,
            GeonisGeaendert = k.GeonisGeaendert,
            UebernommenUtc = jetzt
        };
    }

    private static Dictionary<string, KatasterKennungPosition> Gruppiere(KatasterKennungPlan plan)
    {
        var jeBauteil = new Dictionary<string, KatasterKennungPosition>(StringComparer.OrdinalIgnoreCase);
        foreach (var position in plan.Positionen)
            jeBauteil.TryAdd(position.Bauteil, position);
        return jeBauteil;
    }
}

/// <summary>
/// Formuliert den Plan als Bericht fuer den Bestaetigungsdialog — getrennt von der
/// Rechnung, damit der Text testbar bleibt.
/// </summary>
public static class KatasterKennungBericht
{
    public static string Schreibe(KatasterKennungPlan plan, string quellpfad)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var bauteil = plan.Art == BauteilArt.Haltung ? "Haltungen" : "Schächte";
        var text = new StringBuilder();

        text.AppendLine($"Quelle: {quellpfad}");
        if (!string.IsNullOrWhiteSpace(plan.Stand))
            text.AppendLine($"Stand der Kennungstabelle: {plan.Stand}");
        text.AppendLine($"Geprüft: {plan.GepruefteBauteile} {bauteil} im Projekt.");
        text.AppendLine();

        if (plan.OhneAenderung)
        {
            text.AppendLine("Es gibt nichts zu übernehmen.");
        }
        else
        {
            if (plan.Neu > 0)
            {
                text.AppendLine(
                    $"{plan.Neu.ToString(CultureInfo.InvariantCulture)} {bauteil} " +
                    "würden ihre GEONIS-Kennungen bekommen.");
                if (plan.Gedreht > 0)
                {
                    text.AppendLine(
                        $"    davon {plan.Gedreht} über die Gegenrichtung des Namens gefunden " +
                        "(im Projekt steht der untere Schacht vorn).");
                }
            }

            if (plan.NurAnzeige > 0)
            {
                text.AppendLine(
                    $"{plan.NurAnzeige.ToString(CultureInfo.InvariantCulture)} {bauteil} " +
                    "tragen die Kennung schon; nur das Feld \"GEONIS-Kennung\" wird nachgezogen.");
            }
        }

        var mehrdeutig = plan.Anzahl(KatasterKennungGrund.Mehrdeutig);
        var fehlend = plan.Anzahl(KatasterKennungGrund.NichtGefunden);
        var vorhanden = plan.Anzahl(KatasterKennungGrund.BereitsVorhanden);
        var abweichend = plan.Anzahl(KatasterKennungGrund.Abweichend);

        if (mehrdeutig + fehlend + vorhanden + abweichend > 0)
        {
            text.AppendLine();
            text.AppendLine("Nicht übernommen:");

            if (mehrdeutig > 0)
            {
                text.AppendLine(
                    $"    {mehrdeutig} mit mehrfach vorkommendem Namen — in der Kennungstabelle nicht");
                text.AppendLine("      eindeutig, deshalb wird nichts übernommen.");
            }

            if (fehlend > 0)
                text.AppendLine($"    {fehlend} in der Kennungstabelle nicht gefunden.");

            if (vorhanden > 0)
                text.AppendLine($"    {vorhanden} tragen diese Kennung bereits.");

            if (abweichend > 0)
            {
                text.AppendLine(
                    $"    {abweichend} tragen eine ANDERE GEONIS-Kennung — sie bleibt stehen.");
            }
        }

        text.AppendLine();
        text.AppendLine("Es werden nur Kennungen übernommen, keine Fachwerte.");
        text.AppendLine("Vorhandene Kennungen werden nie überschrieben.");

        return text.ToString().TrimEnd();
    }
}
