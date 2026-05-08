using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.UI.Dialogs;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

// Dropdown-Optionen-Verwaltung (Sanieren, Eigentuemer, Pruefungsresultat,
// Referenzpruefung, Empfohlene Sanierungsmassnahmen): Edit/Preview/Reset
// + Add/Remove + Persistierung via DropdownOptionsStore.
public sealed partial class DataPageViewModel
{
    public void EnsureOptionForField(string fieldName, string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text))
            return;

        if (fieldName == "Sanieren_JaNein")
            AddOptionIfMissing(SanierenOptions, text);
        else if (fieldName == "Eigentuemer")
            return;
        else if (fieldName == "Pruefungsresultat")
            AddOptionIfMissing(PruefungsresultatOptions, text);
        else if (fieldName == "Referenzpruefung")
            AddOptionIfMissing(ReferenzpruefungOptions, text);
        else if (fieldName == "Empfohlene_Sanierungsmassnahmen")
            AddOptionIfMissing(EmpfohleneSanierungsmassnahmenOptions, text);
    }

    private void AddOptionIfMissing(ObservableCollection<string> options, string value)
    {
        if (!AddOptionIfMissingCore(options, value))
            return;
        SaveDropdownOptions();
    }

    private static bool AddOptionIfMissingCore(ObservableCollection<string> options, string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text))
            return false;
        if (options.Any(x => x.Equals(text, StringComparison.OrdinalIgnoreCase)))
            return false;
        options.Insert(0, text);
        return true;
    }

    /// <summary>
    /// Seeds measure template names from Offerten (MeasureTemplateStore) into the dropdown.
    /// Ensures all known template names are available for selection.
    /// </summary>
    private void SeedMeasureTemplateNames()
    {
        try
        {
            var store = new MeasureTemplateStore();
            var catalog = store.LoadMerged(App.Resolve<AppSettings>().LastProjectPath);
            foreach (var measure in catalog.Measures)
            {
                if (measure.Disabled)
                    continue;
                var name = measure.Name?.Trim();
                if (string.IsNullOrEmpty(name))
                    continue;
                if (EmpfohleneSanierungsmassnahmenOptions.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    continue;
                EmpfohleneSanierungsmassnahmenOptions.Add(name);
            }
        }
        catch
        {
            // Non-critical: template seeding failure should not block startup
        }
    }

    private void EditSanierenOptions()
    {
        var vm = new OptionsEditorViewModel(SanierenOptions);
        var dlg = new OptionsEditorWindow(vm);
        if (App.Resolve<IDialogService>().ShowDialog(dlg) == true)
        {
            SanierenOptions.Clear();
            foreach (var item in vm.Items)
                SanierenOptions.Add(item);
            SaveDropdownOptions();
        }
    }

    private void PreviewSanierenOptions()
    {
        var items = string.Join("\n", SanierenOptions);
        _dialogs.ShowMessage(items, "Sanieren-Liste", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    private void ResetSanierenOptions()
    {
        SanierenOptions.Clear();
        foreach (var item in new[] { "Nein", "Ja" })
            SanierenOptions.Add(item);
        SaveDropdownOptions();
    }

    private void AddSanierenOption(object? value)
        => AddOptionIfMissing(SanierenOptions, ExtractText(value));

    private void RemoveSanierenOption(object? value)
        => RemoveOptionFromList(SanierenOptions, ExtractText(value));

    private void EditEigentuemerOptions()
    {
        var vm = new OptionsEditorViewModel(EigentuemerOptions);
        var dlg = new OptionsEditorWindow(vm);
        if (App.Resolve<IDialogService>().ShowDialog(dlg) == true)
        {
            EigentuemerOptions.Clear();
            foreach (var item in vm.Items)
                EigentuemerOptions.Add(item);
            SaveDropdownOptions();
        }
    }

    private void PreviewEigentuemerOptions()
    {
        var items = string.Join("\n", EigentuemerOptions);
        _dialogs.ShowMessage(items, "Eigentuemer-Liste", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    private void ResetEigentuemerOptions()
    {
        EigentuemerOptions.Clear();
        foreach (var item in FixedEigentuemerOptions)
            EigentuemerOptions.Add(item);
        SaveDropdownOptions();
    }

    private void AddEigentuemerOption(object? value)
        => AddOptionIfMissing(EigentuemerOptions, ExtractText(value));

    private void RemoveEigentuemerOption(object? value)
        => RemoveOptionFromList(EigentuemerOptions, ExtractText(value));

    private void EditPruefungsresultatOptions()
    {
        var vm = new OptionsEditorViewModel(PruefungsresultatOptions);
        var dlg = new OptionsEditorWindow(vm);
        if (App.Resolve<IDialogService>().ShowDialog(dlg) == true)
        {
            PruefungsresultatOptions.Clear();
            foreach (var item in vm.Items)
                PruefungsresultatOptions.Add(item);
            SaveDropdownOptions();
        }
    }

    private void PreviewPruefungsresultatOptions()
    {
        var items = string.Join("\n", PruefungsresultatOptions);
        _dialogs.ShowMessage(items, "Pruefungsresultat-Liste", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    private void ResetPruefungsresultatOptions()
    {
        PruefungsresultatOptions.Clear();
        foreach (var item in new[]
                 {
                     "Pruefung bestanden",
                     "Pruefung knapp nicht bestanden",
                     "Pruefung nicht bestanden (grob undicht)",
                     "Keine"
                 })
            PruefungsresultatOptions.Add(item);
        SaveDropdownOptions();
    }

    private void AddPruefungsresultatOption(object? value)
        => AddOptionIfMissing(PruefungsresultatOptions, ExtractText(value));

    private void RemovePruefungsresultatOption(object? value)
        => RemoveOptionFromList(PruefungsresultatOptions, ExtractText(value));

    private void EditReferenzpruefungOptions()
    {
        var vm = new OptionsEditorViewModel(ReferenzpruefungOptions);
        var dlg = new OptionsEditorWindow(vm);
        if (App.Resolve<IDialogService>().ShowDialog(dlg) == true)
        {
            ReferenzpruefungOptions.Clear();
            foreach (var item in vm.Items)
                ReferenzpruefungOptions.Add(item);
            SaveDropdownOptions();
        }
    }

    private void PreviewReferenzpruefungOptions()
    {
        var items = string.Join("\n", ReferenzpruefungOptions);
        _dialogs.ShowMessage(items, "Referenzpruefung-Liste", System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    private void ResetReferenzpruefungOptions()
    {
        ReferenzpruefungOptions.Clear();
        foreach (var item in new[] { "Ja", "Nein" })
            ReferenzpruefungOptions.Add(item);
        SaveDropdownOptions();
    }

    private void AddReferenzpruefungOption(object? value)
        => AddOptionIfMissing(ReferenzpruefungOptions, ExtractText(value));

    private void RemoveReferenzpruefungOption(object? value)
        => RemoveOptionFromList(ReferenzpruefungOptions, ExtractText(value));

    private void EditEmpfohleneSanierungsmassnahmenOptions()
    {
        var vm = new OptionsEditorViewModel(EmpfohleneSanierungsmassnahmenOptions);
        var dlg = new OptionsEditorWindow(vm);
        if (App.Resolve<IDialogService>().ShowDialog(dlg) == true)
        {
            EmpfohleneSanierungsmassnahmenOptions.Clear();
            foreach (var item in vm.Items)
                EmpfohleneSanierungsmassnahmenOptions.Add(item);
            SaveDropdownOptions();
        }
    }

    private void PreviewEmpfohleneSanierungsmassnahmenOptions()
    {
        var items = string.Join("\n", EmpfohleneSanierungsmassnahmenOptions);
        _dialogs.ShowMessage(items, "Sanierungsmassnahmen-Liste", System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    private void ResetEmpfohleneSanierungsmassnahmenOptions()
    {
        EmpfohleneSanierungsmassnahmenOptions.Clear();
        foreach (var item in new[] { "" })
            EmpfohleneSanierungsmassnahmenOptions.Add(item);
        SaveDropdownOptions();
    }

    private void AddEmpfohleneSanierungsmassnahmenOption(object? value)
        => AddOptionIfMissing(EmpfohleneSanierungsmassnahmenOptions, ExtractText(value));

    private void RemoveEmpfohleneSanierungsmassnahmenOption(object? value)
        => RemoveOptionFromList(EmpfohleneSanierungsmassnahmenOptions, ExtractText(value));

    private static string ExtractText(object? value)
    {
        if (value is null)
            return string.Empty;
        if (value is string text)
            return text;
        if (value is System.Windows.Controls.ComboBox combo)
            return combo.Text ?? string.Empty;
        return value.ToString() ?? string.Empty;
    }

    private void RemoveOptionFromList(ObservableCollection<string> options, string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text))
            return;
        var existing = options.FirstOrDefault(x => x.Equals(text, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
            return;
        options.Remove(existing);
        SaveDropdownOptions();
    }

    private void SaveDropdownOptions()
    {
        EnforceEigentuemerOptionsExact();
        SyncDropdownOptionsFromRecords();
        DropdownOptionsStore.SaveSanierenOptions(SanierenOptions);
        DropdownOptionsStore.SaveEigentuemerOptions(EigentuemerOptions);
        DropdownOptionsStore.SavePruefungsresultatOptions(PruefungsresultatOptions);
        DropdownOptionsStore.SaveReferenzpruefungOptions(ReferenzpruefungOptions);
        DropdownOptionsStore.SaveEmpfohleneSanierungsmassnahmenOptions(EmpfohleneSanierungsmassnahmenOptions);
    }

    private void SyncDropdownOptionsFromRecords()
    {
        foreach (var record in Records)
        {
            AddOptionIfMissingCore(SanierenOptions, record.GetFieldValue("Sanieren_JaNein"));
            AddOptionIfMissingCore(PruefungsresultatOptions, record.GetFieldValue("Pruefungsresultat"));
            AddOptionIfMissingCore(ReferenzpruefungOptions, record.GetFieldValue("Referenzpruefung"));

            var recommended = ParseRecommendedTemplates(record.GetFieldValue("Empfohlene_Sanierungsmassnahmen"));
            foreach (var entry in recommended)
                AddOptionIfMissingCore(EmpfohleneSanierungsmassnahmenOptions, entry);
        }
    }

    private void EnforceEigentuemerOptionsExact()
    {
        var same = EigentuemerOptions.Count == FixedEigentuemerOptions.Length;
        if (same)
        {
            for (var i = 0; i < FixedEigentuemerOptions.Length; i++)
            {
                if (!string.Equals(EigentuemerOptions[i], FixedEigentuemerOptions[i], StringComparison.Ordinal))
                {
                    same = false;
                    break;
                }
            }
        }

        if (same)
            return;

        EigentuemerOptions.Clear();
        foreach (var item in FixedEigentuemerOptions)
            EigentuemerOptions.Add(item);
    }

    private static IReadOnlyList<string> ParseRecommendedTemplates(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        return raw.Split(new[] { '\r', '\n', ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeRecommendationEntry)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
