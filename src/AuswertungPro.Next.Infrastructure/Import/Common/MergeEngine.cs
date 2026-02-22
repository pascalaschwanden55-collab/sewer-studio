using System.Text.Json.Nodes;
using AuswertungPro.Next.Domain.Models;

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
    public static MergeResult MergeRecord(HaltungRecord target, HaltungRecord source, FieldSource importSource)
    {
        var res = new MergeResult();

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

                var existing = (target.GetFieldValue(field) ?? "").Trim();

                // Meta holen
                target.FieldMeta.TryGetValue(field, out var meta);
                var userEdited = meta?.UserEdited == true;
                var existingSource = meta?.Source ?? FieldSource.Manual;

                // Wenn Ziel leer -> übernehmen
                if (string.IsNullOrWhiteSpace(existing))
                {
                    target.SetFieldValue(field, incoming, importSource, userEdited: false);
                    res.Updated++;
                    continue;
                }

                // Wenn gleich -> nichts tun
                if (string.Equals(existing, incoming, StringComparison.Ordinal))
                    continue;

                // Wenn UserEdited -> niemals überschreiben, Konflikt loggen
                if (userEdited)
                {
                    AddConflict(res, target, field, existing, existingSource, incoming, importSource, reason: "UserEdited");
                    continue;
                }

                // Wenn Ziel "Manual" war (nicht user-edited) -> Import darf überschreiben
                if (existingSource == FieldSource.Manual)
                {
                    target.SetFieldValue(field, incoming, importSource, userEdited: false);
                    res.Updated++;
                    continue;
                }

                // Wenn gleiche Quelle erneut importiert -> überschreiben (typisch Re-Import)
                if (existingSource == importSource)
                {
                    target.SetFieldValue(field, incoming, importSource, userEdited: false);
                    res.Updated++;
                    continue;
                }

                // Wenn Import-Priorität höher als bestehend -> überschreiben
                if (GetPriority(importSource) > GetPriority(existingSource))
                {
                    target.SetFieldValue(field, incoming, importSource, userEdited: false);
                    res.Updated++;
                    continue;
                }


                // Sonst: Konflikt, nicht überschreiben
                AddConflict(res, target, field, existing, existingSource, incoming, importSource, reason: "DifferentSource");
            }
        }
        catch (Exception)
        {
            res.Errors++;
            // bewusst nicht werfen: Import soll weiterlaufen und Stats zeigen
        }

        return res;
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

