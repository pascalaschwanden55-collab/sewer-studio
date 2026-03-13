using System.Text.Json.Nodes;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Import.Common;

public sealed class MergeResult
{
    public int Updated { get; set; }
    public int Conflicts { get; set; }
    public int Errors { get; set; }
    public List<JsonObject> ConflictDetails { get; } = new();
}

/// <summary>
/// Merge-Logik:
/// - user-edited Werte werden niemals überschrieben
/// - leere Zielwerte werden immer gesetzt
/// - nicht user-edited "Manual" darf von Importen überschrieben werden
/// - Import-Priorität: Xtf/Xtf405 > Ili > Pdf > Legacy > Unknown
/// </summary>
public static class MergeEngine
{
    // Felder die Dateipfade enthalten - werden bei Reimport immer aktualisiert
    private static readonly HashSet<string> PathFields = new(StringComparer.Ordinal)
        { "Link", "PDF_Path", "PDF_All" };

    public static MergeResult MergeRecord(HaltungRecord target, HaltungRecord source, FieldSource importSource, bool fillMissingOnly = false, ImportRunContext? ctx = null)
    {
        var res = new MergeResult();
        var recordKey = (target.GetFieldValue("Haltungsname") ?? "").Trim();

        try
        {
            foreach (var field in FieldCatalog.ColumnOrder)
            {
                // Key-Feld nicht "kaputt-mergen"
                if (string.Equals(field, "Haltungsname", StringComparison.Ordinal))
                    continue;
                // Prüfungsresultat soll nicht aus Importen übernommen werden
                if (string.Equals(field, "Pruefungsresultat", StringComparison.Ordinal))
                    continue;

                var incoming = (source.GetFieldValue(field) ?? "").Trim();
                if (string.IsNullOrWhiteSpace(incoming))
                    continue; // leere Importwerte niemals übernehmen

                // Primaere_Schaeden: Duplikate im Text entfernen (Streckenschaden-Marker etc.)
                if (string.Equals(field, "Primaere_Schaeden", StringComparison.Ordinal))
                    incoming = XtfPrimaryDamageFormatter.DeduplicateText(incoming);

                var existing = (target.GetFieldValue(field) ?? "").Trim();

                // Wenn Ziel leer -> übernehmen (immer, auch bei fillMissingOnly)
                if (string.IsNullOrWhiteSpace(existing))
                {
                    ApplyFieldChange(target, field, incoming, importSource, ctx);
                    ctx?.Log.AddEntry("Merge", "SetField", ImportLogStatus.Updated,
                        recordKey: recordKey, field: field, detail: $"leer -> {incoming}");
                    res.Updated++;
                    continue;
                }

                // "Nur ergaenzen"-Modus: vorhandene Werte nie antasten
                // Ausnahme: Pfad-Felder (Link, PDF_Path, PDF_All) werden immer aktualisiert,
                // weil der Import die authoritative Quelle fuer Medienpfade ist.
                if (fillMissingOnly && !PathFields.Contains(field))
                {
                    ctx?.Log.AddEntry("Merge", "SkipFillMissing", ImportLogStatus.Skipped,
                        recordKey: recordKey, field: field, detail: $"vorhanden: {existing}");
                    continue;
                }

                // Meta holen
                target.FieldMeta.TryGetValue(field, out var meta);
                var userEdited = meta?.UserEdited == true;
                var existingSource = meta?.Source ?? FieldSource.Manual;

                // Wenn gleich -> nichts tun
                if (string.Equals(existing, incoming, StringComparison.Ordinal))
                    continue;

                // Wenn UserEdited -> niemals überschreiben, Konflikt loggen
                if (userEdited)
                {
                    AddConflict(res, target, field, existing, existingSource, incoming, importSource, reason: "UserEdited");
                    ctx?.Log.AddEntry("Merge", "Conflict", ImportLogStatus.Conflict,
                        recordKey: recordKey, field: field,
                        detail: $"UserEdited '{existing}' ({existingSource}) vs '{incoming}' ({importSource})");
                    continue;
                }

                // Wenn Ziel "Manual" war (nicht user-edited) -> Import darf überschreiben
                if (existingSource == FieldSource.Manual)
                {
                    ApplyFieldChange(target, field, incoming, importSource, ctx);
                    ctx?.Log.AddEntry("Merge", "OverwriteManual", ImportLogStatus.Updated,
                        recordKey: recordKey, field: field, detail: $"'{existing}' -> '{incoming}'");
                    res.Updated++;
                    continue;
                }

                // Wenn gleiche Quelle erneut importiert -> überschreiben (typisch Re-Import)
                if (existingSource == importSource)
                {
                    ApplyFieldChange(target, field, incoming, importSource, ctx);
                    ctx?.Log.AddEntry("Merge", "ReImport", ImportLogStatus.Updated,
                        recordKey: recordKey, field: field, detail: $"'{existing}' -> '{incoming}'");
                    res.Updated++;
                    continue;
                }

                // Wenn Import-Priorität höher als bestehend -> überschreiben
                if (GetPriority(importSource) > GetPriority(existingSource))
                {
                    ApplyFieldChange(target, field, incoming, importSource, ctx);
                    ctx?.Log.AddEntry("Merge", "HigherPriority", ImportLogStatus.Updated,
                        recordKey: recordKey, field: field,
                        detail: $"'{existing}' ({existingSource}) -> '{incoming}' ({importSource})");
                    res.Updated++;
                    continue;
                }


                // Sonst: Konflikt, nicht überschreiben
                AddConflict(res, target, field, existing, existingSource, incoming, importSource, reason: "DifferentSource");
                ctx?.Log.AddEntry("Merge", "Conflict", ImportLogStatus.Conflict,
                    recordKey: recordKey, field: field,
                    detail: $"DifferentSource '{existing}' ({existingSource}) vs '{incoming}' ({importSource})");
            }
        }
        catch (Exception)
        {
            res.Errors++;
            ctx?.Log.AddEntry("Merge", "Exception", ImportLogStatus.Error,
                recordKey: recordKey, detail: "Unerwarteter Fehler bei Merge");
            // bewusst nicht werfen: Import soll weiterlaufen und Stats zeigen
        }

        return res;
    }

    private static void ApplyFieldChange(HaltungRecord target, string field, string value, FieldSource source, ImportRunContext? ctx)
    {
        if (ctx?.DryRun != true)
            target.SetFieldValue(field, value, source, userEdited: false);
    }

    private static int GetPriority(FieldSource source) => source switch
    {
        FieldSource.Xtf => 80,
        FieldSource.Xtf405 => 80,
        FieldSource.Ili => 70,
        FieldSource.Pdf => 60,
        FieldSource.Legacy => 50,
        FieldSource.Unknown => 0,
        _ => 0
    };

    private static void AddConflict(
        MergeResult res,
        HaltungRecord target,
        string field,
        string existing,
        FieldSource existingSource,
        string incoming,
        FieldSource incomingSource,
        string reason)
    {
        res.Conflicts++;

        var key = (target.GetFieldValue("Haltungsname") ?? "").Trim();

        res.ConflictDetails.Add(new JsonObject
        {
            ["recordKey"] = key,
            ["field"] = field,
            ["existing"] = existing,
            ["existingSource"] = existingSource.ToString(),
            ["incoming"] = incoming,
            ["incomingSource"] = incomingSource.ToString(),
            ["reason"] = reason,
            ["timestampUtc"] = DateTime.UtcNow.ToString("o")
        });
    }
}

