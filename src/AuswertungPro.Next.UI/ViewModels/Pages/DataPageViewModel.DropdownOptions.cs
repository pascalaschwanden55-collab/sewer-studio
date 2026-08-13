using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class DataPageViewModel
{
    private DataPageDropdownOptionGroups? _optionGroups;

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
        else if (fieldName == FieldKeys.PipeMaterial)
            AddOptionIfMissing(RohrmaterialOptions, text);
    }

    private void AddOptionIfMissing(ObservableCollection<string> options, string value)
    {
        if (!DropdownOptionList.AddIfMissing(options, value))
            return;
        SaveDropdownOptions();
    }

    /// <summary>
    /// Seeds measure template names from Offerten (MeasureTemplateStore) into the dropdown.
    /// Ensures all known template names are available for selection.
    /// </summary>
    private void SeedMeasureTemplateNames()
    {
        try
        {
            var catalog = _measureTemplates.LoadMerged(_settings.LastProjectPath);
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
        => OptionGroups.Sanieren.Edit();

    private void PreviewSanierenOptions()
        => OptionGroups.Sanieren.Preview();

    private void ResetSanierenOptions()
        => OptionGroups.Sanieren.Reset();

    private void AddSanierenOption(object? value)
        => OptionGroups.Sanieren.Add(value);

    private void RemoveSanierenOption(object? value)
        => OptionGroups.Sanieren.Remove(value);

    private void EditEigentuemerOptions()
        => OptionGroups.Eigentuemer.Edit();

    private void PreviewEigentuemerOptions()
        => OptionGroups.Eigentuemer.Preview();

    private void ResetEigentuemerOptions()
        => OptionGroups.Eigentuemer.Reset();

    private void AddEigentuemerOption(object? value)
        => OptionGroups.Eigentuemer.Add(value);

    private void RemoveEigentuemerOption(object? value)
        => OptionGroups.Eigentuemer.Remove(value);

    private void EditPruefungsresultatOptions()
        => OptionGroups.Pruefungsresultat.Edit();

    private void PreviewPruefungsresultatOptions()
        => OptionGroups.Pruefungsresultat.Preview();

    private void ResetPruefungsresultatOptions()
        => OptionGroups.Pruefungsresultat.Reset();

    private void AddPruefungsresultatOption(object? value)
        => OptionGroups.Pruefungsresultat.Add(value);

    private void RemovePruefungsresultatOption(object? value)
        => OptionGroups.Pruefungsresultat.Remove(value);

    private void EditReferenzpruefungOptions()
        => OptionGroups.Referenzpruefung.Edit();

    private void PreviewReferenzpruefungOptions()
        => OptionGroups.Referenzpruefung.Preview();

    private void ResetReferenzpruefungOptions()
        => OptionGroups.Referenzpruefung.Reset();

    private void AddReferenzpruefungOption(object? value)
        => OptionGroups.Referenzpruefung.Add(value);

    private void RemoveReferenzpruefungOption(object? value)
        => OptionGroups.Referenzpruefung.Remove(value);

    private void EditEmpfohleneSanierungsmassnahmenOptions()
        => OptionGroups.EmpfohleneSanierungsmassnahmen.Edit();

    private void PreviewEmpfohleneSanierungsmassnahmenOptions()
        => OptionGroups.EmpfohleneSanierungsmassnahmen.Preview();

    private void ResetEmpfohleneSanierungsmassnahmenOptions()
        => OptionGroups.EmpfohleneSanierungsmassnahmen.Reset();

    private void AddEmpfohleneSanierungsmassnahmenOption(object? value)
        => OptionGroups.EmpfohleneSanierungsmassnahmen.Add(value);

    private void RemoveEmpfohleneSanierungsmassnahmenOption(object? value)
        => OptionGroups.EmpfohleneSanierungsmassnahmen.Remove(value);

    private void EditRohrmaterialOptions()
        => OptionGroups.Rohrmaterial.Edit();

    private void PreviewRohrmaterialOptions()
        => OptionGroups.Rohrmaterial.Preview();

    private void ResetRohrmaterialOptions()
        => OptionGroups.Rohrmaterial.Reset();

    private void AddRohrmaterialOption(object? value)
        => OptionGroups.Rohrmaterial.Add(value);

    // Die festen Katalogwerte sind gesperrt: Wer einen davon entfernt, wuerde
    // importierte Haltungen mit leerem Materialfeld sehen.
    private void RemoveRohrmaterialOption(object? value)
    {
        var text = DropdownOptionList.ExtractText(value);
        if (PipeMaterialOptionList.IsFixed(text))
        {
            _dialogs.Info(
                $"„{text.Trim()}\" ist ein fest eingebautes Rohrmaterial und kann nicht entfernt werden.\n\n" +
                "Der XTF-Import liefert genau diese Schreibweise. Ohne den Eintrag wuerde das Feld bei " +
                "importierten Haltungen leer erscheinen.",
                "Rohrmaterial");
            return;
        }

        OptionGroups.Rohrmaterial.Remove(value);
    }

    private DataPageDropdownOptionGroups OptionGroups
        => _optionGroups ??= DataPageDropdownOptionGroupFactory.Create(
            new DataPageDropdownOptionCollections(
                SanierenOptions,
                EigentuemerOptions,
                PruefungsresultatOptions,
                ReferenzpruefungOptions,
                EmpfohleneSanierungsmassnahmenOptions,
                RohrmaterialOptions),
            _dropdownOptions.FixedEigentuemerOptions,
            new DropdownOptionGroupActions(
                OptionsEditorDialogService.Show,
                _dialogs.Info,
                SaveDropdownOptions));

    private void SaveDropdownOptions()
    {
        EnforceEigentuemerOptionsExact();
        SyncDropdownOptionsFromRecords();
        NormalizeRohrmaterialOptions();
        _dropdownOptions.SaveSanierenOptions(SanierenOptions);
        _dropdownOptions.SaveEigentuemerOptions(EigentuemerOptions);
        _dropdownOptions.SavePruefungsresultatOptions(PruefungsresultatOptions);
        _dropdownOptions.SaveReferenzpruefungOptions(ReferenzpruefungOptions);
        _dropdownOptions.SaveEmpfohleneSanierungsmassnahmenOptions(EmpfohleneSanierungsmassnahmenOptions);
        // Nur die eigenen Ergaenzungen in die Datei; die festen Werte kommen aus dem Feldkatalog.
        _dropdownOptions.SaveRohrmaterialOptions(PipeMaterialOptionList.ExtractCustom(RohrmaterialOptions));
    }

    private void SyncDropdownOptionsFromRecords()
        => DataPageDropdownOptionSynchronizer.SyncFromRecords(
            Records,
            new DataPageDropdownOptionSets(
                SanierenOptions,
                PruefungsresultatOptions,
                ReferenzpruefungOptions,
                EmpfohleneSanierungsmassnahmenOptions,
                RohrmaterialOptions));

    private void EnforceEigentuemerOptionsExact()
    {
        DropdownOptionList.EnsureExact(EigentuemerOptions, _dropdownOptions.FixedEigentuemerOptions);
    }

    /// <summary>
    /// Bringt die Materialliste in die feste Ordnung: Katalogwerte zuerst, eigene danach,
    /// keine Doppelten. Faengt auch ein Zuruecksetzen oder Loeschen im Listen-Editor ab,
    /// bei dem ein Katalogwert verschwunden waere.
    /// </summary>
    private void NormalizeRohrmaterialOptions()
    {
        var composed = PipeMaterialOptionList.Compose(
            PipeMaterialOptionList.ExtractCustom(RohrmaterialOptions));
        if (RohrmaterialOptions.SequenceEqual(composed, StringComparer.Ordinal))
            return;

        DropdownOptionList.ReplaceWith(RohrmaterialOptions, composed);
    }

    private static IReadOnlyList<string> ParseRecommendedTemplates(string? raw)
        => DataPageDropdownOptionSynchronizer.ParseRecommendedTemplates(raw);
}
