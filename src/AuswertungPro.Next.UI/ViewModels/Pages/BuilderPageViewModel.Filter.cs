using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Filterzustand des Druckcenters: Auswahllisten aufbauen, Auswahl zuruecksetzen
/// und die aktuell eingestellten Kriterien buendeln.
///
/// Aus der Hauptdatei hierher verschoben. Sie war mit dem neuen Schacht- und
/// Druckcenter-Stand auf 1079 Zeilen gewachsen und riss damit die 1000er-Grenze
/// des Wartbarkeits-Waechters (Gesamtaudit 2026-08-18, A-01). Der Filter ist der
/// natuerlichste eigenstaendige Teil: Er haengt nur an den Zeilen und den
/// Auswahl-Eigenschaften, an keiner Infrastruktur.
///
/// Kein Verhalten geaendert - nur verschoben.
/// </summary>
public sealed partial class BuilderPageViewModel
{
    /// <summary>Setzt alle Filterwerte zurueck, ohne die Liste neu zu berechnen.</summary>
    private void ResetFilterSelections()
    {
        _suspendFilterRefresh = true;
        try
        {
            SelectedOwnerFilter = AllFilterLabel;
            SelectedExecutedByFilter = AllFilterLabel;
            SelectedSanierenFilter = AllFilterLabel;
            SelectedMaterialFilter = AllFilterLabel;
            SelectedStatusFilter = AllFilterLabel;
            SelectedYearFilter = AllFilterLabel;
            SearchText = "";
            OnlyWithCost = false;
            OnlyWithMeasures = false;
        }
        finally
        {
            _suspendFilterRefresh = false;
        }
    }

    private void InitializeOptionCollections()
    {
        OwnerFilterOptions.Clear();
        ExecutedByFilterOptions.Clear();
        SanierenFilterOptions.Clear();
        MaterialFilterOptions.Clear();
        StatusFilterOptions.Clear();
        YearFilterOptions.Clear();

        OwnerFilterOptions.Add(AllFilterLabel);
        ExecutedByFilterOptions.Add(AllFilterLabel);
        SanierenFilterOptions.Add(AllFilterLabel);
        MaterialFilterOptions.Add(AllFilterLabel);
        StatusFilterOptions.Add(AllFilterLabel);
        YearFilterOptions.Add(AllFilterLabel);
    }

    private void RebuildFilterOptions()
    {
        RebuildOptionCollection(
            OwnerFilterOptions,
            _allRows.Select(r => r.Owner).Where(v => v.Length > 0),
            SelectedOwnerFilter,
            value => SelectedOwnerFilter = value);

        var executedByValues = _allRows
            .Select(r => r.ExecutedBy)
            .Where(v => v.Length > 0)
            .Concat(DefaultExecutedByValues);

        if (!string.IsNullOrWhiteSpace(SelectedExecutedByFilter) &&
            !SelectedExecutedByFilter.Equals(AllFilterLabel, StringComparison.OrdinalIgnoreCase))
        {
            executedByValues = executedByValues.Concat(new[] { SelectedExecutedByFilter.Trim() });
        }

        RebuildOptionCollection(
            ExecutedByFilterOptions,
            executedByValues,
            SelectedExecutedByFilter,
            value => SelectedExecutedByFilter = value);

        RebuildOptionCollection(
            SanierenFilterOptions,
            _allRows.Select(r => r.Sanieren).Where(v => v.Length > 0),
            SelectedSanierenFilter,
            value => SelectedSanierenFilter = value);

        RebuildOptionCollection(
            MaterialFilterOptions,
            _allRows.Select(r => r.Material).Where(v => v.Length > 0),
            SelectedMaterialFilter,
            value => SelectedMaterialFilter = value);

        RebuildOptionCollection(
            StatusFilterOptions,
            _allRows.Select(r => r.Status).Where(v => v.Length > 0),
            SelectedStatusFilter,
            value => SelectedStatusFilter = value);

        RebuildOptionCollection(
            YearFilterOptions,
            _allRows.Select(r => r.Year).Where(v => v.Length > 0),
            SelectedYearFilter,
            value => SelectedYearFilter = value);
    }

    private static void RebuildOptionCollection(
        ObservableCollection<string> target,
        IEnumerable<string> values,
        string selected,
        Action<string> setSelected)
    {
        var allValues = values
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();

        target.Clear();
        target.Add(AllFilterLabel);
        foreach (var value in allValues)
            target.Add(value);

        if (target.Contains(selected))
            setSelected(selected);
        else
            setSelected(AllFilterLabel);
    }

    /// <summary>Die aktuell eingestellten Filterkriterien.</summary>
    private BuilderPageFilterCriteria CurrentFilterCriteria()
        => new(
            SelectedOwnerFilter,
            SelectedExecutedByFilter,
            SelectedSanierenFilter,
            SelectedMaterialFilter,
            SelectedStatusFilter,
            SelectedYearFilter,
            SearchText,
            OnlyWithCost,
            OnlyWithMeasures);

    /// <summary>
    /// Zeilen fuer den Ausdruck. Haltungen und Schaechte werden getrennt gedruckt:
    /// Der gewaehlte Bereich bestimmt den Inhalt, also zwei Bauteilarten = zwei Dokumente.
    /// Das ist die sichtbare Liste — was am Bildschirm steht, kommt auch aufs Papier.
    /// </summary>
    public List<DruckcenterRowVm> BuildExportRows()
        => Rows.ToList();

    /// <summary>
    /// Dateiname des Ausdrucks. Er nennt die Bauteilart, damit die getrennten PDFs
    /// nebeneinander liegen koennen, statt sich gegenseitig zu ueberschreiben.
    /// </summary>
    public string BuildExportFileName()
    {
        var bauteil = Bereich == DruckcenterRowKind.Schacht ? "Schaechte" : "Haltungen";
        var projekt = SanitizeFilePart(_shell.Project.Name);
        return $"Druckcenter_{bauteil}_{projekt}_{DateTime.Now:yyyyMMdd}.pdf";
    }
}
