using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Pages;
using AuswertungPro.Next.UI.Views.Windows;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.Views.Pages;

// DataPage HaltungRecord-Details: Vollanzeige aller Felder einer Haltung in
// gruppierter Form (Stammdaten, Zustand, Sanierung, Dokumente, Weitere) im
// RecordDetailsWindow. Inkl. Managed-Combo-Aufloesung und Commit-Logik.
// Aus dem Hauptdatei extrahiert (Slice 6d).
public partial class DataPage
{
    private void ShowHaltungRecordDetails(HaltungRecord record)
    {
        var holding = record.GetFieldValue("Haltungsname");
        var header = string.IsNullOrWhiteSpace(holding)
            ? "Haltungsdetails"
            : $"Haltung {holding}";

        var subtitle = "Komplette Zeile in Spaltenreihenfolge der Haltungs-Ansicht.";
        var groups = BuildHaltungRecordDetails(record);

        ICommand? suggestCmd = null;
        if (DataContext is DataPageViewModel vm)
        {
            suggestCmd = new RelayCommand(() => vm.OpenCostsCommand.Execute(record));
        }

        var window = new RecordDetailsWindow(
            title: string.IsNullOrWhiteSpace(holding) ? "Haltungsdetails" : $"Haltungsdetails - {holding}",
            header: header,
            subHeader: subtitle,
            groups: groups,
            suggestMeasuresCommand: suggestCmd)
        {
            Owner = Window.GetWindow(this)
        };
        window.Show();
    }

    private List<RecordDetailGroup> BuildHaltungRecordDetails(HaltungRecord record)
    {
        var groups = new List<RecordDetailGroup>();
        var added = new HashSet<string>(StringComparer.Ordinal);
        var buckets = new Dictionary<string, List<RecordDetailItem>>(StringComparer.Ordinal)
        {
            ["Stammdaten"] = new(),
            ["Zustand & Inspektion"] = new(),
            ["Sanierung & Kosten"] = new(),
            ["Dokumente & Medien"] = new(),
            ["Weitere Angaben"] = new()
        };

        foreach (var column in FieldCatalog.ColumnOrder.Where(x => added.Add(x)))
        {
            var groupName = ResolveHaltungDetailGroup(column);
            buckets[groupName].Add(CreateHaltungDetailItem(column, record));
        }

        foreach (var extraField in record.Fields.Keys
                     .Where(x => !added.Contains(x))
                     .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            buckets["Weitere Angaben"].Add(CreateHaltungDetailItem(extraField, record));
        }

        AddHaltungGroup(groups, buckets, "Stammdaten", "Identifikation und Lage der Haltung.");
        AddHaltungGroup(groups, buckets, "Zustand & Inspektion", "Bewertung, Schaeden und Pruefresultate.");
        AddHaltungGroup(groups, buckets, "Sanierung & Kosten", "Massnahmen, Kosten und Mengenangaben.");
        AddHaltungGroup(groups, buckets, "Dokumente & Medien", "Verknuepfte Dateien, PDFs und Links.");
        AddHaltungGroup(groups, buckets, "Weitere Angaben", "Felder ohne klare Zuordnung.");

        return groups;
    }

    private RecordDetailItem CreateHaltungDetailItem(string fieldName, HaltungRecord record)
    {
        var def = FieldCatalog.Get(fieldName);
        var label = def.Label;
        var value = record.GetFieldValue(fieldName);

        // Managed combo fields (ViewModel-driven dropdowns)
        var managedCombo = ResolveManagedComboSpec(fieldName);
        if (managedCombo is not null)
        {
            return new RecordDetailItem(
                label,
                value,
                commitValue: next => CommitHaltungDetailField(record, fieldName, next),
                isCombo: true,
                allowFreeText: managedCombo.Value.AllowFreeText,
                options: managedCombo.Value.Options,
                editOptionsCommand: managedCombo.Value.EditCmd,
                previewOptionsCommand: managedCombo.Value.PreviewCmd,
                resetOptionsCommand: managedCombo.Value.ResetCmd,
                addOptionCommand: managedCombo.Value.AddCmd,
                removeOptionCommand: managedCombo.Value.RemoveCmd);
        }

        // Catalog combo fields
        var catalogItems = FieldCatalog.GetComboItems(fieldName);
        if (catalogItems.Count > 0)
        {
            return new RecordDetailItem(
                label,
                value,
                commitValue: next => CommitHaltungDetailField(record, fieldName, next),
                isCombo: true,
                allowFreeText: false,
                options: catalogItems);
        }

        var isMultiline = fieldName is "Primaere_Schaeden" or "Bemerkungen" or "Empfohlene_Sanierungsmassnahmen";
        var digitsOnly = def.Type == FieldType.Int;

        return new RecordDetailItem(
            label,
            value,
            commitValue: next => CommitHaltungDetailField(record, fieldName, next),
            isMultiline: isMultiline,
            digitsOnly: digitsOnly);
    }

    private (IEnumerable<string> Options, bool AllowFreeText,
        ICommand? EditCmd, ICommand? PreviewCmd, ICommand? ResetCmd,
        ICommand? AddCmd, ICommand? RemoveCmd)? ResolveManagedComboSpec(string fieldName)
    {
        if (DataContext is not DataPageViewModel vm)
            return null;

        return fieldName switch
        {
            "Sanieren_JaNein" => (vm.SanierenOptions, true,
                vm.EditSanierenOptionsCommand, vm.PreviewSanierenOptionsCommand,
                vm.ResetSanierenOptionsCommand, null, null),
            "Eigentuemer" => (vm.EigentuemerOptions, false,
                vm.EditEigentuemerOptionsCommand, vm.PreviewEigentuemerOptionsCommand,
                vm.ResetEigentuemerOptionsCommand, null, null),
            "Pruefungsresultat" => (vm.PruefungsresultatOptions, true,
                vm.EditPruefungsresultatOptionsCommand, vm.PreviewPruefungsresultatOptionsCommand,
                vm.ResetPruefungsresultatOptionsCommand, null, null),
            "Referenzpruefung" => (vm.ReferenzpruefungOptions, true,
                vm.EditReferenzpruefungOptionsCommand, vm.PreviewReferenzpruefungOptionsCommand,
                vm.ResetReferenzpruefungOptionsCommand, null, null),
            _ => null
        };
    }

    private void CommitHaltungDetailField(HaltungRecord record, string fieldName, string? value)
    {
        var next = value ?? string.Empty;
        record.SetFieldValue(fieldName, next, FieldSource.Manual, userEdited: true);

        if (DataContext is DataPageViewModel vm)
        {
            vm.EnsureOptionForField(fieldName, next);
            vm.ScheduleAutoSave();
        }
    }

    private static void AddHaltungGroup(
        ICollection<RecordDetailGroup> groups,
        IReadOnlyDictionary<string, List<RecordDetailItem>> buckets,
        string title,
        string description)
    {
        if (!buckets.TryGetValue(title, out var items) || items.Count == 0)
            return;

        groups.Add(new RecordDetailGroup(title, description, items));
    }

    private static string ResolveHaltungDetailGroup(string fieldName)
    {
        return fieldName switch
        {
            "NR" or "Haltungsname" or "Strasse" or "DN_mm" or "Rohrmaterial"
                or "Nutzungsart" or "Haltungslaenge_m" or "Inspektionsrichtung"
                or "Eigentuemer" or "FunktionHierarchisch"
                => "Stammdaten",

            "Zustandsklasse" or "VSA_Zustandsnote_D" or "VSA_Zustandsnote_S"
                or "VSA_Zustandsnote_B" or "Primaere_Schaeden" or "Pruefungsresultat"
                or "Referenzpruefung" or "Datum_Jahr" or "Ausgefuehrt_durch"
                or "Gewaesserschutz" or "Grundwasserspiegel"
                => "Zustand & Inspektion",

            "Sanieren_JaNein" or "Empfohlene_Sanierungsmassnahmen" or "Kosten"
                or "Renovierung_Inliner_Stk" or "Renovierung_Inliner_m"
                or "Anschluesse_verpressen" or "Reparatur_Manschette"
                or "Linerendmanschette_LEM"
                or "Reparatur_Kurzliner" or "Erneuerung_Neubau_m"
                or "Offen_abgeschlossen"
                => "Sanierung & Kosten",

            "Link" => "Dokumente & Medien",

            _ => "Weitere Angaben"
        };
    }
}
