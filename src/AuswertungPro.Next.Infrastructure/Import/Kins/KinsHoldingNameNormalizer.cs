using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import.Kins;

/// <summary>Ergebnis der Haltungsnamen-Normalisierung.</summary>
public sealed record KinsNameNormalizeResult(
    IReadOnlyDictionary<string, HaltungRecord> RecordsProBezeichnung,
    int Umbenannt,
    int DuplikateEntfernt,
    IReadOnlyList<string> Messages);

/// <summary>
/// Nach dem XTF-Import: KINS schreibt als Untersuchungs-Bezeichnung eine
/// laufende Nummer ("10", "18", …). Damit die Haltung wie ueberall sonst
/// "{Schacht_oben}-{Schacht_unten}" heisst, benennt dieser Schritt rein
/// numerische Haltungsnamen um. Die Original-Bezeichnung wird im dynamischen
/// Feld KINS_Bezeichnung gemerkt — sie ist der Schluessel fuer die
/// Einzelprotokoll-PDFs (Haltung&lt;N&gt;.pdf) und macht Re-Importe idempotent
/// (der Zweitlauf erzeugt sonst ein "10"-Duplikat neben "58951-58950").
/// </summary>
public static class KinsHoldingNameNormalizer
{
    /// <summary>Feld, in dem die originale KINS-Bezeichnung aufbewahrt wird.</summary>
    public const string BezeichnungsFeld = "KINS_Bezeichnung";

    public static KinsNameNormalizeResult Apply(Project project, ImportRunContext? ctx = null)
    {
        var map = new Dictionary<string, HaltungRecord>(StringComparer.OrdinalIgnoreCase);
        var messages = new List<string>();
        var umbenannt = 0;
        var entfernt = 0;

        // Bereits normalisierte Records (Vorlauf) anhand des gemerkten Feldes einsammeln.
        foreach (var record in project.Data)
        {
            var bez = (record.GetFieldValue(BezeichnungsFeld) ?? "").Trim();
            if (bez.Length > 0)
                map[bez] = record;
        }

        // Snapshot, weil Re-Import-Duplikate entfernt werden koennen.
        foreach (var record in project.Data.ToList())
        {
            var name = (record.GetFieldValue("Haltungsname") ?? "").Trim();
            if (name.Length == 0 || !name.All(char.IsDigit))
                continue; // nur numerische KINS-Bezeichnungen anfassen

            var oben = (record.GetFieldValue("Schacht_oben") ?? "").Trim();
            var unten = (record.GetFieldValue("Schacht_unten") ?? "").Trim();
            if (oben.Length == 0 || unten.Length == 0)
            {
                map[name] = record; // kein Schachtpaar → Name bleibt, PDF-Zuordnung trotzdem moeglich
                continue;
            }

            var zielName = $"{oben}-{unten}";
            var bestehend = project.Data.FirstOrDefault(r =>
                !ReferenceEquals(r, record) &&
                string.Equals(Normalisiere(r.GetFieldValue("Haltungsname")), Normalisiere(zielName), StringComparison.OrdinalIgnoreCase));

            if (bestehend is null)
            {
                record.SetFieldValue("Haltungsname", zielName, FieldSource.Xtf, userEdited: false);
                record.SetFieldValue(BezeichnungsFeld, name, FieldSource.Legacy, userEdited: false);
                map[name] = record;
                umbenannt++;
                continue;
            }

            var bestehendeBezeichnung = (bestehend.GetFieldValue(BezeichnungsFeld) ?? "").Trim();
            if (string.Equals(bestehendeBezeichnung, name, StringComparison.OrdinalIgnoreCase) && !HatUserEdit(record))
            {
                // Re-Import derselben Untersuchung: Der frische Nummern-Record ist ein
                // Import-Duplikat des bereits normalisierten Records — entfernen.
                if (ctx is null)
                    project.Data.Remove(record);
                else
                    ctx.WithCollectionLock(() => project.Data.Remove(record));
                map[name] = bestehend;
                entfernt++;
                messages.Add($"KINS: Re-Import-Duplikat der Bezeichnung '{name}' entfernt ({zielName}).");
                continue;
            }

            // Echte Kollision (z.B. zweite Untersuchung desselben Schachtpaars):
            // Nummer behalten, damit nichts verloren geht.
            map[name] = record;
            messages.Add($"KINS: Name {zielName} existiert bereits — '{name}' behaelt die Nummer.");
        }

        return new KinsNameNormalizeResult(map, umbenannt, entfernt, messages);
    }

    private static bool HatUserEdit(HaltungRecord record)
        => record.FieldMeta.Values.Any(m => m.UserEdited);

    private static string Normalisiere(string? wert)
        => string.IsNullOrWhiteSpace(wert)
            ? string.Empty
            : wert.Trim().Replace(" ", string.Empty).ToUpperInvariant();
}
