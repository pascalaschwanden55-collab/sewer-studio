using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Objektakten;

/// <summary>Eigene OBJECTID bevorzugen; die gewünschte Bezeichnungsanzeige verändert keine XTF-Kennung.</summary>
public static class SchachtObjektId
{
    public static (string Wert, bool AusBezeichnung) Anzeige(ObjektaktenBearbeitung bearbeitung, ObjektAkte akte)
    {
        if (akte.Werte.TryGetValue("schacht.objectid", out var id) && (id.VonHand || !string.IsNullOrWhiteSpace(id.Text)))
            return (id.Text, false);
        return (bearbeitung.Lies(akte, FieldCatalog.Objektfelder.Feld("schacht.bezeichnung")), true);
    }

    public static string? AusQuelle(ObjektQuellbeleg? quelle)
    {
        var ids = quelle?.Werte.Where(p => p.Key.Equals("OBJECTID", StringComparison.OrdinalIgnoreCase)
                || p.Key.Equals("OBJID", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Value.Trim()).Where(v => v.Length > 0).Distinct(StringComparer.Ordinal).ToArray() ?? [];
        if (ids.Length > 1)
            throw new InvalidOperationException("OBJID und OBJECTID derselben Quelle widersprechen sich. Bitte die Kennung prüfen.");
        return ids.SingleOrDefault();
    }
}
