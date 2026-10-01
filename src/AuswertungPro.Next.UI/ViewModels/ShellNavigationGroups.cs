using System.Collections.Generic;

namespace AuswertungPro.Next.UI.ViewModels;

/// <summary>
/// Gruppen der linken Leiste aus dem Nova-Prototyp: Projekt, Daten, Bewertung, System.
/// Reine Zuordnung nach Titel; die Reihenfolge innerhalb einer Gruppe bleibt die der NavItems.
/// </summary>
public static class ShellNavigationGroups
{
    public static IReadOnlyList<string> Order { get; } = ["Projekt", "Daten", "Bewertung", "System"];

    public static string GroupOf(string? title) => title switch
    {
        "Uebersicht" or "Projekt" or "Haltungen" or "Schaechte" => "Projekt",
        "Import" or "Export" or "Medienkonflikte" or "Druckcenter" or "Dossiers" => "Daten",
        "Sanierungs-Matrix" or "Schacht-Matrix" or "Schattenauswertung" or "VSA" => "Bewertung",
        _ => "System"
    };
}
