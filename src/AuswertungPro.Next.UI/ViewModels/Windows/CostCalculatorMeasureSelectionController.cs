namespace AuswertungPro.Next.UI.ViewModels.Windows;

public sealed class CostCalculatorMeasureSelectionController
{
    private readonly Dictionary<string, int> _measureOrderById = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selectedMeasureIds = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> SelectedMeasureIds => _selectedMeasureIds;

    public void ReplaceMeasureOrder(IEnumerable<string?> measureIds)
    {
        ArgumentNullException.ThrowIfNull(measureIds);

        _measureOrderById.Clear();
        var order = 0;
        foreach (var rawId in measureIds)
        {
            var id = rawId?.Trim();
            if (!string.IsNullOrWhiteSpace(id) && !_measureOrderById.ContainsKey(id))
                _measureOrderById[id] = order;

            order++;
        }
    }

    public void SetSelectedMeasures(IEnumerable<MeasureTemplateListItem> measures)
    {
        ArgumentNullException.ThrowIfNull(measures);

        _selectedMeasureIds.Clear();
        foreach (var measure in measures)
        {
            if (measure.Disabled)
                continue;

            _selectedMeasureIds.Add(measure.Id);
        }
    }

    public IReadOnlyList<MeasureBlockVm> OrderMeasures(IEnumerable<MeasureBlockVm> measures)
    {
        ArgumentNullException.ThrowIfNull(measures);

        return measures
            .Select((measure, index) => new
            {
                Measure = measure,
                Index = index,
                Order = GetMeasureOrder(measure),
                Name = measure.MeasureName ?? string.Empty
            })
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Index)
            .Select(x => x.Measure)
            .ToList();
    }

    private int GetMeasureOrder(MeasureBlockVm? measure)
    {
        if (measure is null || string.IsNullOrWhiteSpace(measure.MeasureId))
            return int.MaxValue;

        return _measureOrderById.TryGetValue(measure.MeasureId.Trim(), out var order)
            ? order
            : int.MaxValue;
    }
}
