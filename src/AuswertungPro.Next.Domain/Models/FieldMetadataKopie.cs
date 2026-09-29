using System.Text.Json.Nodes;

namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Kopie und Vergleich von <see cref="FieldMetadata"/> fuer Rueckgaengig/Wiederholen (Optik Aufgabe 16).
/// Eine Kopie ist noetig, weil die normalen Schreibwege die vorhandenen Metadaten eines Feldes an Ort
/// und Stelle aendern: Ein gemerkter Verweis wuerde sonst still mitwandern.
/// </summary>
public static class FieldMetadataKopie
{
    public static FieldMetadata Von(FieldMetadata meta)
    {
        ArgumentNullException.ThrowIfNull(meta);
        return new FieldMetadata
        {
            FieldName = meta.FieldName,
            Source = meta.Source,
            UserEdited = meta.UserEdited,
            LastUpdatedUtc = meta.LastUpdatedUtc,
            Conflict = meta.Conflict?.DeepClone() as JsonObject,
        };
    }

    /// <summary>Gleiche Herkunft, Handmarke und Konfliktnotiz; der Zeitstempel zaehlt nur auf Wunsch.</summary>
    public static bool Gleich(FieldMetadata? a, FieldMetadata? b, bool mitZeitstempel)
    {
        if (a is null || b is null)
            return a is null && b is null;
        return a.Source == b.Source
               && a.UserEdited == b.UserEdited
               && string.Equals(a.FieldName, b.FieldName, StringComparison.Ordinal)
               && (!mitZeitstempel || a.LastUpdatedUtc == b.LastUpdatedUtc)
               && string.Equals(a.Conflict?.ToJsonString(), b.Conflict?.ToJsonString(), StringComparison.Ordinal);
    }
}
