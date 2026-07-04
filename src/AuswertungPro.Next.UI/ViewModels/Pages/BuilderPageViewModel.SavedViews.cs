using System.Text.Json;
using AuswertungPro.Next.UI.Behaviors;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Dim4: Der Druckcenter-Filter als serialisierbarer Teil einer gespeicherten Ansicht.
/// Spalten und Sortierung erfasst der SavedViewsController generisch ueber das Grid.
/// </summary>
public sealed partial class BuilderPageViewModel : ISavedViewFilterProvider
{
    private static readonly JsonSerializerOptions SavedViewFilterJson = new() { PropertyNameCaseInsensitive = true };

    public string? CaptureFilterState()
        => JsonSerializer.Serialize(
            new BuilderPageFilterCriteria(
                SelectedOwnerFilter,
                SelectedExecutedByFilter,
                SelectedSanierenFilter,
                SelectedMaterialFilter,
                SelectedStatusFilter,
                SelectedYearFilter,
                SearchText,
                OnlyWithCost,
                OnlyWithMeasures),
            SavedViewFilterJson);

    public void ApplyFilterState(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
            return;

        BuilderPageFilterCriteria? criteria;
        try
        {
            criteria = JsonSerializer.Deserialize<BuilderPageFilterCriteria>(state, SavedViewFilterJson);
        }
        catch (JsonException)
        {
            return; // beschaedigte gespeicherte Ansicht ignorieren
        }

        if (criteria is null)
            return;

        // Gleiches Muster wie ResetFilters: erst alle Felder setzen, dann EINMAL filtern.
        _suspendFilterRefresh = true;
        try
        {
            SelectedOwnerFilter = criteria.Owner ?? AllFilterLabel;
            SelectedExecutedByFilter = criteria.ExecutedBy ?? AllFilterLabel;
            SelectedSanierenFilter = criteria.Sanieren ?? AllFilterLabel;
            SelectedMaterialFilter = criteria.Material ?? AllFilterLabel;
            SelectedStatusFilter = criteria.Status ?? AllFilterLabel;
            SelectedYearFilter = criteria.Year ?? AllFilterLabel;
            SearchText = criteria.Search ?? "";
            OnlyWithCost = criteria.OnlyWithCost;
            OnlyWithMeasures = criteria.OnlyWithMeasures;
        }
        finally
        {
            _suspendFilterRefresh = false;
        }

        ApplyFilters();
    }
}
