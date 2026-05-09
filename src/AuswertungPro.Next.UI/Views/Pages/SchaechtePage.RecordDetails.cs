using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Pages;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Views.Pages;

// SchaechtePage Schacht-Detail-Dialog: Vollanzeige aller Felder eines
// Schachts gruppiert in Stammdaten/Lage/Zustand/Sanierung/Weitere.
// Aus dem Hauptdatei extrahiert (Slice 10c).
public partial class SchaechtePage
{
    private void ShowRecordDetails(SchachtRecord record)
    {
        var schacht = GetSchachtNumber(record);
        var header = string.IsNullOrWhiteSpace(schacht)
            ? "Schachtdetails"
            : $"Schacht {schacht}";

        var subtitle = "Komplette Zeile in Spaltenreihenfolge der Schacht-Ansicht.";
        var groups = BuildRecordDetails(record);
        var window = new RecordDetailsWindow(
            title: string.IsNullOrWhiteSpace(schacht) ? "Schachtdetails" : $"Schachtdetails - {schacht}",
            header: header,
            subHeader: subtitle,
            groups: groups)
        {
            Owner = Window.GetWindow(this)
        };
        window.Show();
    }

    private List<RecordDetailGroup> BuildRecordDetails(SchachtRecord record)
    {
        var groups = new List<RecordDetailGroup>();
        var added = new HashSet<string>(StringComparer.Ordinal);
        var buckets = new Dictionary<string, List<RecordDetailItem>>(StringComparer.Ordinal)
        {
            ["Stammdaten"] = new(),
            ["Zustand und Inspektion"] = new(),
            ["Sanierung und Kosten"] = new(),
            ["Dokumente und Medien"] = new(),
            ["Weitere Angaben"] = new()
        };

        if (_vm is not null)
        {
            foreach (var column in _vm.Columns.Where(x => added.Add(x)))
            {
                var groupName = ResolveSchachtDetailGroup(column);
                buckets[groupName].Add(CreateSchachtDetailItem(column, record));
            }
        }

        foreach (var extraField in record.Fields.Keys
                     .Where(x => !added.Contains(x))
                     .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            buckets["Weitere Angaben"].Add(CreateSchachtDetailItem(extraField, record));
        }

        AddSchachtGroup(groups, buckets, "Stammdaten", "Identifikation und Lage des Schachts.");
        AddSchachtGroup(groups, buckets, "Zustand und Inspektion", "Bewertung, Schaeden und Pruefresultate.");
        AddSchachtGroup(groups, buckets, "Sanierung und Kosten", "Massnahmen, Kosten und Mengenangaben.");
        AddSchachtGroup(groups, buckets, "Dokumente und Medien", "Verknuepfte Dateien, PDFs und Links.");
        AddSchachtGroup(groups, buckets, "Weitere Angaben", "Felder ohne klare Zuordnung.");

        return groups;
    }

    private RecordDetailItem CreateSchachtDetailItem(string fieldName, SchachtRecord record)
    {
        var label = GetDisplayHeader(fieldName);
        var value = record.GetFieldValue(fieldName);

        if (_vm is not null && TryResolveDropdownColumnSpec(fieldName, out var spec))
        {
            var options = ResolveOptions(spec.ItemsSourcePath);
            return new RecordDetailItem(
                label,
                value,
                commitValue: next => CommitSchachtDetailField(record, fieldName, next),
                isCombo: true,
                allowFreeText: spec.AllowFreeText,
                options: options,
                editOptionsCommand: spec.Managed ? ResolveViewModelCommand(spec.EditCommand) : null,
                previewOptionsCommand: spec.Managed ? ResolveViewModelCommand(spec.PreviewCommand) : null,
                resetOptionsCommand: spec.Managed ? ResolveViewModelCommand(spec.ResetCommand) : null,
                addOptionCommand: spec.Managed ? ResolveViewModelCommand(spec.AddCommand) : null,
                removeOptionCommand: spec.Managed ? ResolveViewModelCommand(spec.RemoveCommand) : null);
        }

        var normalized = Normalize(fieldName);
        var isMultiline = IsPrimaryDamagesColumn(fieldName)
                          || normalized.Contains("bemerk", StringComparison.Ordinal);
        var digitsOnly = IsZustandsklasseColumn(fieldName);

        return new RecordDetailItem(
            label,
            value,
            commitValue: next => CommitSchachtDetailField(record, fieldName, next),
            isMultiline: isMultiline,
            digitsOnly: digitsOnly);
    }

    private IEnumerable<string> ResolveOptions(string itemsSourcePath)
    {
        if (_vm is null)
            return Array.Empty<string>();

        return itemsSourcePath switch
        {
            "SanierenOptions" => _vm.SanierenOptions,
            "EigentuemerOptions" => _vm.EigentuemerOptions,
            "PruefungsresultatOptions" => _vm.PruefungsresultatOptions,
            "ReferenzpruefungOptions" => _vm.ReferenzpruefungOptions,
            "AusgefuehrtDurchOptions" => _vm.AusgefuehrtDurchOptions,
            _ => Array.Empty<string>()
        };
    }

    private ICommand? ResolveViewModelCommand(string propertyName)
    {
        if (_vm is null || string.IsNullOrWhiteSpace(propertyName))
            return null;

        return _vm.GetType().GetProperty(propertyName)?.GetValue(_vm) as ICommand;
    }

    private void CommitSchachtDetailField(SchachtRecord record, string recordField, string? value)
    {
        var next = value ?? string.Empty;
        string? oldShaftNumber = null;
        if (string.Equals(recordField, "Schachtnummer", StringComparison.Ordinal))
            oldShaftNumber = record.GetFieldValue("Schachtnummer");

        record.SetFieldValue(recordField, next);

        if (string.Equals(recordField, "Schachtnummer", StringComparison.Ordinal))
            PdfCorrectionMetadata.RegisterShaftRename(GetCurrentProject(), oldShaftNumber, next);

        if (_vm is not null)
        {
            var optionField = ResolveOptionField(recordField);
            if (!string.IsNullOrWhiteSpace(optionField))
                _vm.EnsureOptionForField(optionField, next);
        }

        MarkProjectDirty();
        ApplySearchFilter();
    }

    private static void AddSchachtGroup(
        ICollection<RecordDetailGroup> groups,
        IReadOnlyDictionary<string, List<RecordDetailItem>> buckets,
        string title,
        string description)
    {
        if (!buckets.TryGetValue(title, out var items) || items.Count == 0)
            return;

        groups.Add(new RecordDetailGroup(title, description, items));
    }

    private static string ResolveSchachtDetailGroup(string columnName)
    {
        var normalized = Normalize(columnName);

        if (normalized.Contains("kosten", StringComparison.Ordinal) ||
            normalized.Contains("sanier", StringComparison.Ordinal) ||
            normalized.Contains("renovierung", StringComparison.Ordinal) ||
            normalized.Contains("reparatur", StringComparison.Ordinal) ||
            normalized.Contains("erneuerung", StringComparison.Ordinal) ||
            normalized.Contains("anschluss", StringComparison.Ordinal))
            return "Sanierung und Kosten";

        if (normalized.Contains("pdf", StringComparison.Ordinal) ||
            normalized.Contains("link", StringComparison.Ordinal) ||
            normalized.Contains("video", StringComparison.Ordinal) ||
            normalized.Contains("film", StringComparison.Ordinal) ||
            normalized.Contains("datei", StringComparison.Ordinal))
            return "Dokumente und Medien";

        if (normalized.Contains("zustand", StringComparison.Ordinal) ||
            normalized.Contains("schaden", StringComparison.Ordinal) ||
            normalized.Contains("pruefung", StringComparison.Ordinal) ||
            normalized.Contains("dicht", StringComparison.Ordinal) ||
            normalized.Contains("referenz", StringComparison.Ordinal) ||
            normalized.Contains("gewaesser", StringComparison.Ordinal) ||
            normalized.Contains("grundwasser", StringComparison.Ordinal))
            return "Zustand und Inspektion";

        if (normalized.Contains("schacht", StringComparison.Ordinal) ||
            normalized.Contains("nummer", StringComparison.Ordinal) ||
            normalized.Contains("name", StringComparison.Ordinal) ||
            normalized.Contains("nr", StringComparison.Ordinal) ||
            normalized.Contains("funktion", StringComparison.Ordinal) ||
            normalized.Contains("strasse", StringComparison.Ordinal) ||
            normalized.Contains("lage", StringComparison.Ordinal) ||
            normalized.Contains("ort", StringComparison.Ordinal) ||
            normalized.Contains("material", StringComparison.Ordinal) ||
            normalized.Contains("dn", StringComparison.Ordinal) ||
            normalized.Contains("durchmesser", StringComparison.Ordinal) ||
            normalized.Contains("eigentuem", StringComparison.Ordinal) ||
            normalized.Contains("eigentum", StringComparison.Ordinal))
            return "Stammdaten";

        return "Weitere Angaben";
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T target)
                return target;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static bool TryGetEditedTextValue(FrameworkElement? element, out string value)
    {
        if (element is ComboBox combo)
        {
            value = ResolveComboBoxValue(combo);
            return true;
        }

        if (element is TextBox textBox)
        {
            value = textBox.Text ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }
}
