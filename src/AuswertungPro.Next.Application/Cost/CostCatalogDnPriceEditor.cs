using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Cost;

/// <summary>
/// Kleine reine Helfer zum Bearbeiten der DN-Preisliste einer ByDN-Position im Katalog-Editor.
/// Bewusst ohne WPF, damit die Logik (Vorschlag der naechsten DN-Zeile) testbar bleibt.
/// </summary>
public static class CostCatalogDnPriceEditor
{
    /// <summary>
    /// Erzeugt eine sinnvolle neue DN-Zeile: DN knapp oberhalb der bisher groessten, Preis 0.
    /// Leere Liste -> DN 100. Der Nutzer passt DN/Preis danach an.
    /// </summary>
    public static DnPrice CreateNextRow(IReadOnlyList<DnPrice> existing)
    {
        var maxDn = existing is { Count: > 0 } ? existing.Max(p => p.DnTo) : 0;
        var next = maxDn <= 0 ? 100 : maxDn + 50;
        return new DnPrice { DnFrom = next, DnTo = next, Price = 0m };
    }
}
