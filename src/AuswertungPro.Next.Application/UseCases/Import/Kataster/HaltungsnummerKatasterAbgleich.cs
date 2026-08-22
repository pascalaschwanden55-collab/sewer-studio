using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Import.Kataster;

/// <summary>Ergebnis des Abgleichs gegen den amtlichen Kataster.</summary>
public sealed record KatasterAbgleichErgebnis(
    int Geprueft,
    int Korrigiert,
    int Uebersprungen,
    IReadOnlyList<string> Meldungen)
{
    public static KatasterAbgleichErgebnis Leer { get; } =
        new(0, 0, 0, Array.Empty<string>());
}

/// <summary>
/// Gleicht die Haltungsnummern eines Projekts gegen den amtlichen Kataster ab.
///
/// Grundlage bleibt immer das Inspektionsprotokoll: Die Nummer wird aus Schacht oben und
/// Schacht unten gebildet. Liegt zusaetzlich eine amtliche Katasterdatei vor und fuehrt
/// diese fuer dasselbe Schachtpaar eine andere Bezeichnung, wird die amtliche uebernommen
/// und die Aenderung sichtbar gemeldet.
///
/// Bewusst konservativ:
/// - Ohne beide Schaechte wird nichts geaendert.
/// - Ein vom Benutzer bearbeiteter Haltungsname bleibt unangetastet.
/// - Waere der amtliche Name schon von einer ANDEREN Haltung belegt, wird nicht
///   umbenannt, sondern der Konflikt gemeldet. Zwei Haltungen duerfen nie denselben
///   Namen bekommen.
///
/// Reine Anwendungslogik: kein Dateizugriff, keine KI. Das Lesen der Katasterdatei liegt
/// in Infrastructure.
/// </summary>
public static class HaltungsnummerKatasterAbgleich
{
    public static KatasterAbgleichErgebnis Gleiche(
        Project project,
        IKatasterHaltungsverzeichnis? kataster)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (kataster is null || kataster.Anzahl == 0)
            return KatasterAbgleichErgebnis.Leer;

        var meldungen = new List<string>();
        var geprueft = 0;
        var korrigiert = 0;
        var uebersprungen = 0;

        // Belegte Namen vorab erfassen, damit eine Umbenennung keinen zweiten Datensatz
        // mit demselben Namen erzeugt.
        var belegt = new Dictionary<string, HaltungRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in project.Data)
        {
            var n = r.GetFieldValue(FieldKeys.HoldingName);
            if (!string.IsNullOrWhiteSpace(n))
                belegt[n.Trim()] = r;
        }

        foreach (var record in project.Data.ToList())
        {
            var name = record.GetFieldValue(FieldKeys.HoldingName)?.Trim();
            var oben = record.GetFieldValue("Schacht_oben")?.Trim();
            var unten = record.GetFieldValue("Schacht_unten")?.Trim();

            if (string.IsNullOrWhiteSpace(oben) || string.IsNullOrWhiteSpace(unten))
                continue;

            geprueft++;

            var amtlich = kataster.FindeBezeichnung(oben, unten);
            if (string.IsNullOrWhiteSpace(amtlich)
                || string.Equals(amtlich, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IstVomBenutzerBearbeitet(record))
            {
                uebersprungen++;
                meldungen.Add(
                    $"Haltung {name}: amtlich {amtlich} — von Hand bearbeiteter Name bleibt unveraendert.");
                continue;
            }

            if (belegt.TryGetValue(amtlich!, out var anderer) && !ReferenceEquals(anderer, record))
            {
                uebersprungen++;
                meldungen.Add(
                    $"Haltung {name}: amtlich {amtlich}, dieser Name ist aber bereits vergeben. "
                    + "Nicht umbenannt — bitte pruefen.");
                continue;
            }

            record.SetFieldValue(FieldKeys.HoldingName, amtlich, FieldSource.Xtf, userEdited: false);
            if (!string.IsNullOrWhiteSpace(name))
                belegt.Remove(name);
            belegt[amtlich!] = record;

            korrigiert++;
            meldungen.Add($"Haltung {name} heisst amtlich {amtlich} (Schacht {oben} -> {unten}).");
        }

        if (korrigiert > 0 || uebersprungen > 0)
        {
            meldungen.Insert(0,
                $"Katasterabgleich: {geprueft} Haltung(en) geprueft, {korrigiert} umbenannt, "
                + $"{uebersprungen} offen.");
        }

        return new KatasterAbgleichErgebnis(geprueft, korrigiert, uebersprungen, meldungen);
    }

    private static bool IstVomBenutzerBearbeitet(HaltungRecord record)
        => record.FieldMeta.TryGetValue(FieldKeys.HoldingName, out var meta) && meta.UserEdited;
}
