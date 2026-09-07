using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>Spaltensaetze der Schachtliste (Inventar 4.4). Reine Daten wie DataPageColumnViewCatalog.</summary>
public static class SchaechteColumnViewCatalog
{
    private const string Nummer = "Schachtnummer";

    public static IReadOnlyList<DataPageColumnView> Views { get; } =
    [
        // Nova-Etappe 2b: Das Protokoll steht als Knopf (virtuelle Spalte), nicht als roher
        // Dateipfad. Bearbeitet wird der Pfad in "Alle Spalten" und in den Eingabefeldern.
        new("kompakt", "Kompakt", [Nummer, "Strasse", "Funktion", "Material", FieldKeys.ShaftDimension1Mm, FieldKeys.ShaftDimension2Mm, FieldKeys.ShaftShape, FieldKeys.ConditionClass, NovaStatusSpalten.Protokoll]),
        new("zustand", "Zustand und Inspektion", [Nummer, FieldKeys.ConditionClass, "Pruefungsresultat", "Dichtheit", "Referenzpruefung", "Gewaesserschutz", "Grundwasserspiegel", FieldKeys.LoadClass, "Inspektionsdatum"]),
        new("sanierung", "Sanierung und Kosten", [Nummer, FieldKeys.RenovationDecision, FieldKeys.RecommendedRehabilitationMeasures, FieldKeys.RehabilitationExecutor, FieldKeys.WorkflowStatus, FieldKeys.Cost, "Eigentümer"]),
        new("medien", "Dokumente und Medien", [Nummer, NovaStatusSpalten.Protokoll, FieldKeys.PdfEigen, FieldKeys.Link, "Fotos"]),
        new("alle", "Alle Spalten", null)
    ];

    public static DataPageColumnView Resolve(string? key)
        => Views.FirstOrDefault(v => string.Equals(v.Key, key, StringComparison.OrdinalIgnoreCase)) ?? Views[^1];
}
