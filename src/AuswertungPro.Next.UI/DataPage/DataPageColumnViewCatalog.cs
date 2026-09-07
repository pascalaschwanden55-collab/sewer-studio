using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>Eine gespeicherte Spaltenansicht der Haltungsliste. Felder == null bedeutet alle Spalten.</summary>
public sealed record DataPageColumnView(string Key, string Titel, IReadOnlyList<string>? Felder)
{
    /// <summary>Spaltenzahl dieser Ansicht; ohne eigene Feldliste gilt die Gesamtzahl aller Spalten.</summary>
    public int Anzahl(int alleSpalten) => Felder?.Count ?? alleSpalten;

    /// <summary>
    /// Nova-Fixwelle F2: Zaehlt nur Felder, welche die Tabelle wirklich als Spalte fuehrt.
    /// Der Chip darf keine Spalte versprechen, die es in diesem Projekt gar nicht gibt.
    /// <paramref name="falte"/> ist der Namensvergleich der Liste (bei Schaechten
    /// <c>SchachtFeldnamen.Falte</c>, sonst ein reiner Ordinalvergleich).
    /// </summary>
    public int Anzahl(IReadOnlyCollection<string> vorhandeneFelder, Func<string, string>? falte = null)
    {
        ArgumentNullException.ThrowIfNull(vorhandeneFelder);
        if (Felder is null)
            return vorhandeneFelder.Count;

        var norm = falte ?? (name => name);
        var vorhanden = new HashSet<string>(vorhandeneFelder.Select(norm), StringComparer.Ordinal);
        return Felder.Count(feld => vorhanden.Contains(norm(feld)));
    }

    /// <summary>Gehoert die Spalte <paramref name="feld"/> zu dieser Ansicht?</summary>
    public bool Enthaelt(string feld, Func<string, string>? falte = null)
    {
        if (Felder is null)
            return true;

        var norm = falte ?? (name => name);
        var gesucht = norm(feld ?? string.Empty);
        return Felder.Any(f => string.Equals(norm(f), gesucht, StringComparison.Ordinal));
    }
}

/// <summary>
/// Feste Spaltensaetze aus dem Nova-Prototyp. Reine Daten, keine WPF-Abhaengigkeit: Der
/// Controller blendet Spalten nur ein oder aus; Werte und Export bleiben unberuehrt.
/// </summary>
public static class DataPageColumnViewCatalog
{
    public static IReadOnlyList<DataPageColumnView> Views { get; } =
    [
        // Nova-Etappe 2b: Kompakt ist genau der Prototyp v2 — sechs Feldspalten und die vier
        // virtuellen Statusspalten. Der frueher hier gefuehrte rohe Videopfad (FieldKeys.Link)
        // ist durch die Spalte "Video" ersetzt; bearbeitbar bleibt er in "Alle Spalten".
        new("kompakt", "Kompakt", [FieldKeys.HoldingName, FieldKeys.Street, FieldKeys.PipeMaterial, FieldKeys.NominalDiameterMm, FieldKeys.HoldingLengthMeters, FieldKeys.ConditionClass, NovaStatusSpalten.Ki, NovaStatusSpalten.Pruefung, NovaStatusSpalten.Video, NovaStatusSpalten.Protokoll]),
        new("stammdaten", "Stammdaten", [FieldKeys.HoldingName, FieldKeys.Street, FieldKeys.PipeMaterial, FieldKeys.NominalDiameterMm, FieldKeys.ClearWidthMm, FieldKeys.ProfileType, FieldKeys.UsageType, FieldKeys.HoldingLengthMeters, FieldKeys.InspectionYear, FieldKeys.ConstructionYear, FieldKeys.Owner, FieldKeys.GeonisId, FieldKeys.CadastreObjectId]),
        new("bewertung", "Bewertung", [FieldKeys.HoldingName, FieldKeys.ConditionClass, "VSA_Zustandsnote_D", "VSA_Zustandsnote_S", "VSA_Zustandsnote_B", "VSA_Geschaetzt", "Pruefungsresultat", "Referenzpruefung", "Gewaesserschutz", "Grundwasserspiegel", NovaStatusSpalten.Pruefung]),
        new("sanierung", "Sanierung", [FieldKeys.HoldingName, FieldKeys.RenovationDecision, FieldKeys.RecommendedRehabilitationMeasures, FieldKeys.LinerRenovationMeters, FieldKeys.ConnectionsToGrout, FieldKeys.RepairSleeve, FieldKeys.LinerEndSleeve, FieldKeys.ShortLinerRepair, "Erneuerung_Neubau_m", FieldKeys.RehabilitationExecutor, FieldKeys.WorkflowStatus]),
        new("kosten", "Kosten", [FieldKeys.HoldingName, FieldKeys.Street, FieldKeys.ConditionClass, FieldKeys.RecommendedRehabilitationMeasures, FieldKeys.Cost, FieldKeys.RehabilitationExecutor, FieldKeys.WorkflowStatus, FieldKeys.Owner]),
        new("alle", "Alle Spalten", null)
    ];

    /// <summary>
    /// Fix-Runde 1 (F5): Dieselben Ansichten fuer die alte Haltungsansicht. Dort gibt es die vier
    /// virtuellen Statusspalten nicht — "Kompakt" fuehrt deshalb weiter den rohen Videopfad
    /// <c>Link</c>, sonst waere die Videoangabe dort ersatzlos verschwunden.
    /// </summary>
    public static IReadOnlyList<DataPageColumnView> AltansichtViews { get; } =
        Views.Select(OhneVirtuelleSpalten).ToList();

    /// <summary>Die Ansichten des aktiven Layouts.</summary>
    public static IReadOnlyList<DataPageColumnView> ViewsFuer(bool nova) => nova ? Views : AltansichtViews;

    public static DataPageColumnView Resolve(string? key) => Resolve(key, nova: true);

    public static DataPageColumnView Resolve(string? key, bool nova)
    {
        var views = ViewsFuer(nova);
        return views.FirstOrDefault(v => string.Equals(v.Key, key, StringComparison.OrdinalIgnoreCase)) ?? views[^1];
    }

    private static DataPageColumnView OhneVirtuelleSpalten(DataPageColumnView view)
    {
        if (view.Felder is null)
            return view;

        var felder = view.Felder.Where(f => !NovaStatusSpalten.IstVirtuell(f)).ToList();
        if (string.Equals(view.Key, "kompakt", StringComparison.Ordinal))
            felder.Add(FieldKeys.Link);

        return view with { Felder = felder };
    }
}
