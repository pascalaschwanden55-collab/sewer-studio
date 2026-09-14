using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>Normwerte und vorhandene Angaben der Objektmaske gemeinsam anzeigen; keine neue DSS-Zuordnung.</summary>
internal static class SanierungsbedarfOptionen
{
    public static IReadOnlyList<string> Alle { get; } = FieldCatalog.GetComboItems(FieldKeys.RehabilitationNeed)
        .Concat(FieldCatalog.Objektfelder.Auswahl(FieldCatalog.Objektfelder.Feld("schacht.sanierungsbedarf").KatalogId)!
            .Eintraege.Select(e => e.Label))
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
