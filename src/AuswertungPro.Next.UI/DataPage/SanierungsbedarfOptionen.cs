using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>Normwerte und vorhandene Angaben der Objektmaske gemeinsam anzeigen; keine neue DSS-Zuordnung.</summary>
internal static class SanierungsbedarfOptionen
{
    // Seit 23.09.2026 die WebGIS-Liste selbst (sie enthaelt «Saniert»); keine zweite Liste.
    public static IReadOnlyList<string> Alle { get; } = FieldCatalog.GetComboItems(FieldKeys.RehabilitationNeed);
}
