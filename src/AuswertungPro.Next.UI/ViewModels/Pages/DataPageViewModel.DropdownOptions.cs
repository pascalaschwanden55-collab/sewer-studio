using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Dialogs;
using AuswertungPro.Next.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

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
        if (!DropdownOptionList.AddIfMissing(options, value))
            return;
        SaveDropdownOptions();
    }

    private static bool AddOptionIfMissingCore(ObservableCollection<string> options, string? value)
        => DropdownOptionList.AddIfMissing(options, value);

    /// <summary>
    /// Seeds measure template names from Offerten (MeasureTemplateStore) into the dropdown.
    /// Ensures all known template names are available for selection.
    /// </summary>
    private void SeedMeasureTemplateNames()
    {
        try
        {
            var store = new MeasureTemplateStore();
            var catalog = store.LoadMerged(_settings.LastProjectPath);
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
        if (dlg.ShowDialog() == true)
        {
            DropdownOptionList.ReplaceWith(SanierenOptions, vm.Items);
            SaveDropdownOptions();
        }
    }

    private void PreviewSanierenOptions()
    {
        var items = string.Join("\n", SanierenOptions);
        _dialogs.Info(items, "Sanieren-Liste");
    }

    private void ResetSanierenOptions()
    {
        DropdownOptionList.ReplaceWith(SanierenOptions, new[] { "Nein", "Ja" });
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
        if (dlg.ShowDialog() == true)
        {
            DropdownOptionList.ReplaceWith(EigentuemerOptions, vm.Items);
            SaveDropdownOptions();
        }
    }

    private void PreviewEigentuemerOptions()
    {
        var items = string.Join("\n", EigentuemerOptions);
        _dialogs.Info(items, "Eigentuemer-Liste");
    }

    private void ResetEigentuemerOptions()
    {
        DropdownOptionList.ReplaceWith(EigentuemerOptions, _dropdownOptions.FixedEigentuemerOptions);
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
        if (dlg.ShowDialog() == true)
        {
            DropdownOptionList.ReplaceWith(PruefungsresultatOptions, vm.Items);
            SaveDropdownOptions();
        }
    }

    private void PreviewPruefungsresultatOptions()
    {
        var items = string.Join("\n", PruefungsresultatOptions);
        _dialogs.Info(items, "Pruefungsresultat-Liste");
    }

    private void ResetPruefungsresultatOptions()
    {
        DropdownOptionList.ReplaceWith(
            PruefungsresultatOptions,
            new[]
            {
                "Pruefung bestanden",
                "Pruefung knapp nicht bestanden",
                "Pruefung nicht bestanden (grob undicht)",
                "Keine"
            });
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
        if (dlg.ShowDialog() == true)
        {
            DropdownOptionList.ReplaceWith(ReferenzpruefungOptions, vm.Items);
            SaveDropdownOptions();
        }
    }

    private void PreviewReferenzpruefungOptions()
    {
        var items = string.Join("\n", ReferenzpruefungOptions);
        _dialogs.Info(items, "Referenzpruefung-Liste");
    }

    private void ResetReferenzpruefungOptions()
    {
        DropdownOptionList.ReplaceWith(ReferenzpruefungOptions, new[] { "Ja", "Nein" });
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
        if (dlg.ShowDialog() == true)
        {
            DropdownOptionList.ReplaceWith(EmpfohleneSanierungsmassnahmenOptions, vm.Items);
            SaveDropdownOptions();
        }
    }

    private void PreviewEmpfohleneSanierungsmassnahmenOptions()
    {
        var items = string.Join("\n", EmpfohleneSanierungsmassnahmenOptions);
        _dialogs.Info(items, "Sanierungsmassnahmen-Liste");
    }

    private void ResetEmpfohleneSanierungsmassnahmenOptions()
    {
        DropdownOptionList.ReplaceWith(EmpfohleneSanierungsmassnahmenOptions, new[] { "" });
        SaveDropdownOptions();
    }

    private void AddEmpfohleneSanierungsmassnahmenOption(object? value)
        => AddOptionIfMissing(EmpfohleneSanierungsmassnahmenOptions, ExtractText(value));

    private void RemoveEmpfohleneSanierungsmassnahmenOption(object? value)
        => RemoveOptionFromList(EmpfohleneSanierungsmassnahmenOptions, ExtractText(value));

    private static string ExtractText(object? value)
        => DropdownOptionList.ExtractText(value);

    private void RemoveOptionFromList(ObservableCollection<string> options, string? value)
    {
        if (DropdownOptionList.Remove(options, value))
            SaveDropdownOptions();
    }

    private void SaveDropdownOptions()
    {
        EnforceEigentuemerOptionsExact();
        SyncDropdownOptionsFromRecords();
        _dropdownOptions.SaveSanierenOptions(SanierenOptions);
        _dropdownOptions.SaveEigentuemerOptions(EigentuemerOptions);
        _dropdownOptions.SavePruefungsresultatOptions(PruefungsresultatOptions);
        _dropdownOptions.SaveReferenzpruefungOptions(ReferenzpruefungOptions);
        _dropdownOptions.SaveEmpfohleneSanierungsmassnahmenOptions(EmpfohleneSanierungsmassnahmenOptions);
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
        DropdownOptionList.EnsureExact(EigentuemerOptions, _dropdownOptions.FixedEigentuemerOptions);
    }

    private static IReadOnlyList<string> ParseRecommendedTemplates(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        return raw.Split(new[] { '\r', '\n', ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(DataPageSanierungCostMapper.NormalizeRecommendationEntry)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
