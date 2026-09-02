using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases;

/// <summary>
/// Schreibt einen <see cref="LeereFelderPlan"/> in die Datensaetze — und sonst nichts.
///
/// Gleiches Muster wie beim XTF-Export: Der Ausfuehrer trifft keine eigenen
/// Entscheidungen mehr. Er prueft nur noch einmal, dass das Zielfeld wirklich leer
/// ist; zwischen Planung und Bestaetigung kann der Bearbeiter etwas eingetippt
/// haben, und dessen Arbeit gewinnt immer.
/// </summary>
public static class LeereFelderAnwender
{
    /// <summary>
    /// Die Herkunft, mit der nachgefuellte Werte gespeichert werden.
    ///
    /// <c>userEdited</c> bleibt bewusst <b>false</b>: Ein aus dem Kataster geholter
    /// Wert ist keine Handeingabe. Waere er als solche markiert, ginge er beim
    /// naechsten Mal als "vom Operateur von Hand gesetzt" in die revidierte XTF
    /// zurueck — in dieselbe Quelle, aus der er stammt.
    /// </summary>
    public const FieldSource Herkunft = FieldSource.Kataster;

    public static int WendeAnAufHaltungen(IEnumerable<HaltungRecord> haltungen, LeereFelderPlan plan)
    {
        ArgumentNullException.ThrowIfNull(haltungen);
        ArgumentNullException.ThrowIfNull(plan);

        var jeBauteil = Gruppiere(plan);
        var geschrieben = 0;

        foreach (var record in haltungen)
        {
            var name = (record.GetFieldValue(FieldKeys.HoldingName) ?? "").Trim();
            if (name.Length == 0 || !jeBauteil.TryGetValue(name, out var positionen))
                continue;

            foreach (var position in positionen)
            {
                if (!string.IsNullOrWhiteSpace(record.GetFieldValue(position.Feld)))
                    continue;

                record.SetFieldValue(position.Feld, position.Wert, Herkunft, userEdited: false);
                geschrieben++;
            }
        }

        return geschrieben;
    }

    public static int WendeAnAufSchaechte(IEnumerable<SchachtRecord> schaechte, LeereFelderPlan plan)
    {
        ArgumentNullException.ThrowIfNull(schaechte);
        ArgumentNullException.ThrowIfNull(plan);

        var jeBauteil = Gruppiere(plan);
        var geschrieben = 0;

        foreach (var record in schaechte)
        {
            var name = (record.GetFieldValue("Schachtnummer") ?? "").Trim();
            if (name.Length == 0 || !jeBauteil.TryGetValue(name, out var positionen))
                continue;

            foreach (var position in positionen)
            {
                if (!string.IsNullOrWhiteSpace(record.GetFieldValue(position.Feld)))
                    continue;

                record.SetFieldValue(position.Feld, position.Wert, Herkunft, userEdited: false);
                geschrieben++;
            }
        }

        return geschrieben;
    }

    private static Dictionary<string, List<LeereFeldPosition>> Gruppiere(LeereFelderPlan plan)
        => plan.Positionen
            .GroupBy(p => p.Bauteil, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Formuliert den Plan als Bericht fuer den Bestaetigungsdialog.
///
/// Getrennt vom Planbauer, damit die Rechnung nicht von der Formulierung abhaengt —
/// und damit der Text testbar bleibt.
/// </summary>
public static class LeereFelderBericht
{
    public static string Schreibe(LeereFelderPlan plan, string quellpfad)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var bauteil = plan.Art == BauteilArt.Haltung ? "Haltungen" : "Schaechte";
        var text = new StringBuilder();

        text.AppendLine($"Quelle: {quellpfad}");
        text.AppendLine($"Geprueft: {plan.GepruefteBauteile} {bauteil} im Projekt.");
        text.AppendLine();

        if (plan.OhneAenderung)
        {
            text.AppendLine("Es gibt nichts zu ergaenzen — alle Felder sind entweder gefuellt");
            text.AppendLine("oder im QGIS-Bestand ohne Angabe.");
        }
        else
        {
            text.AppendLine(
                $"{plan.Positionen.Count} leere Felder auf {plan.BetroffeneBauteile} {bauteil} " +
                "wuerden ergaenzt:");
            foreach (var (feld, anzahl) in plan.JeFeld)
                text.AppendLine($"    {anzahl.ToString(CultureInfo.InvariantCulture),6}x  {feld}");
        }

        var mehrdeutig = plan.Anzahl(LeerfeldGrund.Mehrdeutig);
        var fehlend = plan.Anzahl(LeerfeldGrund.NichtGefunden);
        var nichts = plan.Anzahl(LeerfeldGrund.NichtsZuErgaenzen);

        if (mehrdeutig + fehlend + nichts > 0)
        {
            text.AppendLine();
            text.AppendLine("Nicht ergaenzt:");

            if (mehrdeutig > 0)
            {
                text.AppendLine(
                    $"    {mehrdeutig} mit mehrfach vorkommendem Namen — im QGIS-Bestand nicht");
                text.AppendLine("      eindeutig, deshalb wird nichts uebernommen.");
            }

            if (fehlend > 0)
                text.AppendLine($"    {fehlend} im QGIS-Bestand nicht gefunden.");

            if (nichts > 0)
                text.AppendLine($"    {nichts} ohne offene Luecke.");
        }

        text.AppendLine();
        text.AppendLine("Gefuellte Felder werden nie ueberschrieben.");

        return text.ToString().TrimEnd();
    }
}
