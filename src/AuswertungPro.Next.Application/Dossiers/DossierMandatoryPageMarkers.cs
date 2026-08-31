using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Die unsichtbaren Seitenmarken aller automatisch erzeugten Dossierblaetter.
///
/// Ohne sie waere ein Pflichtblatt nur an seinem sichtbaren Titel erkennbar —
/// und eine Kundenbeilage mit demselben Titel wuerde faelschlich als
/// unloeschbares Blatt gelten. Die Marken stehen 1 pt gross und weiss in der
/// Fusszeile: im Ausdruck unsichtbar, im PDF-Text eindeutig.
///
/// WPF-frei, damit Vorschau, Ausgabe und Seitenauswahl dieselbe Regel lesen.
/// </summary>
public static class DossierMandatoryPageMarkers
{
    /// <summary>Das feste einseitige Erklaerblatt zu den Zustandsklassen.</summary>
    public const string ConditionClassExplanation =
        DossierConditionClassDefinitions.PdfRequiredPageMarker;

    /// <summary>Jede Seite der frisch erzeugten Haltungsliste.</summary>
    public const string HoldingList =
        "SEWERSTUDIO_DOSSIER_HALTUNGSLISTE_PFLICHTBLATT_V1";

    /// <summary>Jede Seite der frisch erzeugten Schachtliste.</summary>
    public const string ShaftList =
        "SEWERSTUDIO_DOSSIER_SCHACHTLISTE_PFLICHTBLATT_V1";

    public const string ConditionClassExplanationLabel = "Dossier-Erklärung";
    public const string HoldingListLabel = "Haltungsliste";
    public const string ShaftListLabel = "Schachtliste";

    private static readonly IReadOnlyList<(string Marker, string Label)> Known =
        new ReadOnlyCollection<(string, string)>(
        [
            (ConditionClassExplanation, ConditionClassExplanationLabel),
            (HoldingList, HoldingListLabel),
            (ShaftList, ShaftListLabel)
        ]);

    /// <summary>
    /// Die Beschriftung des Pflichtblatts, dessen Marke im Seitentext steht —
    /// oder <c>null</c> fuer eine gewoehnliche Seite.
    /// </summary>
    public static string? FindLabel(string? pageText)
    {
        if (string.IsNullOrEmpty(pageText))
            return null;

        foreach (var (marker, label) in Known)
        {
            if (pageText.Contains(marker, StringComparison.Ordinal))
                return label;
        }

        return null;
    }

    /// <summary>Ob die Seite ein automatisch erzeugtes Pflichtblatt ist.</summary>
    public static bool IsMandatoryPage(string? pageText) => FindLabel(pageText) is not null;
}
