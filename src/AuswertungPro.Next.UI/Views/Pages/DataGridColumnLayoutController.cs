using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Views.Pages;

public sealed class DataGridColumnLayoutController
{
    private readonly Dictionary<DataGridColumn, HorizontalAlignment> _columnHorizontalAlignments = new();
    private readonly Dictionary<DataGridColumn, VerticalAlignment> _columnVerticalAlignments = new();
    private readonly Dictionary<DataGridColumn, Style?> _baseCellStyles = new();
    private readonly Dictionary<DataGridTextColumn, Style?> _baseTextElementStyles = new();
    private readonly Dictionary<DataGridTextColumn, Style?> _baseTextEditingStyles = new();
    private readonly HashSet<DataGridColumn> _trackedColumns = new();
    private bool _isRestoring;

    public event EventHandler? LayoutChanged;

    public bool IsRestoring => _isRestoring;

    public void Clear()
    {
        foreach (var column in _trackedColumns)
            DetachColumnLayoutChangeHandlers(column);

        _trackedColumns.Clear();
        _columnHorizontalAlignments.Clear();
        _columnVerticalAlignments.Clear();
        _baseCellStyles.Clear();
        _baseTextElementStyles.Clear();
        _baseTextEditingStyles.Clear();
    }

    public HorizontalAlignment GetHorizontalAlignment(DataGridColumn column)
    {
        if (_columnHorizontalAlignments.TryGetValue(column, out var value))
            return value;
        return HorizontalAlignment.Left;
    }

    public VerticalAlignment GetVerticalAlignment(DataGridColumn column)
    {
        if (_columnVerticalAlignments.TryGetValue(column, out var value))
            return value;
        return VerticalAlignment.Center;
    }

    public void SetAlignment(
        DataGridColumn column,
        HorizontalAlignment horizontalAlignment,
        VerticalAlignment verticalAlignment)
    {
        _columnHorizontalAlignments[column] = horizontalAlignment;
        _columnVerticalAlignments[column] = verticalAlignment;

        ApplyCellAlignment(column, horizontalAlignment, verticalAlignment);

        if (column is DataGridTextColumn textColumn)
            ApplyTextColumnAlignment(textColumn, horizontalAlignment, verticalAlignment);

        NotifyLayoutChanged();
    }

    public void Restore(
        IEnumerable<DataGridColumn> columns,
        DataPageLayoutSettings? layout,
        Action<IReadOnlyList<DataGridColumn>>? adjustOrder = null)
    {
        var columnList = columns.Cast<DataGridColumn>().ToList();

        _isRestoring = true;
        try
        {
            foreach (var column in columnList)
                AttachColumnLayoutChangeHandlers(column);

            if (layout is null)
            {
                adjustOrder?.Invoke(columnList);
                return;
            }

            var byField = layout.Columns?
                .Where(c => !string.IsNullOrWhiteSpace(c.FieldName))
                .GroupBy(c => c.FieldName, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal)
                ?? new Dictionary<string, DataPageColumnLayout>(StringComparer.Ordinal);

            foreach (var column in columnList)
            {
                if (column.GetValue(FrameworkElement.TagProperty) is not string fieldName)
                    continue;
                if (!byField.TryGetValue(fieldName, out var state))
                    continue;

                if (state.WidthValue > 0 &&
                    Enum.TryParse<DataGridLengthUnitType>(state.WidthUnitType, out var widthType))
                {
                    column.Width = new DataGridLength(state.WidthValue, widthType);
                }

                var horizontal = ParseHorizontalAlignment(state.HorizontalAlignment);
                var vertical = ParseVerticalAlignment(state.VerticalAlignment);
                SetAlignment(column, horizontal, vertical);
            }

            var orderedColumns = columnList
                .Select(column =>
                {
                    var field = column.GetValue(FrameworkElement.TagProperty) as string;
                    if (field is not null && byField.TryGetValue(field, out var state))
                        return new { Column = column, Target = state.DisplayIndex, HasState = true };
                    return new { Column = column, Target = column.DisplayIndex, HasState = false };
                })
                .OrderBy(x => x.HasState ? 0 : 1)
                .ThenBy(x => x.Target)
                .ToList();

            for (var i = 0; i < orderedColumns.Count; i++)
            {
                try
                {
                    orderedColumns[i].Column.DisplayIndex = i;
                }
                catch
                {
                    // WPF can reject transient DisplayIndex moves while columns are being rebuilt.
                }
            }

            adjustOrder?.Invoke(columnList);
        }
        finally
        {
            _isRestoring = false;
        }
    }

    public DataPageLayoutSettings Capture(IEnumerable<DataGridColumn> columns)
    {
        return new DataPageLayoutSettings
        {
            Columns = columns
                .Cast<DataGridColumn>()
                .Select(column =>
                {
                    var fieldName = column.GetValue(FrameworkElement.TagProperty) as string ?? "";
                    return new DataPageColumnLayout
                    {
                        FieldName = fieldName,
                        DisplayIndex = column.DisplayIndex,
                        WidthValue = column.Width.Value,
                        WidthUnitType = column.Width.UnitType.ToString(),
                        HorizontalAlignment = GetHorizontalAlignment(column).ToString(),
                        VerticalAlignment = GetVerticalAlignment(column).ToString()
                    };
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.FieldName))
                .ToList()
        };
    }

    public static void EnsureFieldBefore(
        IEnumerable<DataGridColumn> columns,
        string fieldName,
        string followingFieldName)
    {
        var columnList = columns.Cast<DataGridColumn>().ToList();
        var first = FindColumnByFieldName(columnList, fieldName);
        var following = FindColumnByFieldName(columnList, followingFieldName);
        if (first is null || following is null)
            return;

        if (first.DisplayIndex < following.DisplayIndex)
            return;

        try
        {
            var target = following.DisplayIndex;
            first.DisplayIndex = target;
            following.DisplayIndex = target + 1;
        }
        catch
        {
            // WPF can reject transient DisplayIndex moves while columns are being rebuilt.
        }
    }

    private void ApplyCellAlignment(
        DataGridColumn column,
        HorizontalAlignment horizontalAlignment,
        VerticalAlignment verticalAlignment)
    {
        if (!_baseCellStyles.ContainsKey(column))
            _baseCellStyles[column] = column.CellStyle;

        var style = new Style(typeof(DataGridCell), _baseCellStyles[column]);
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, horizontalAlignment));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, verticalAlignment));
        column.CellStyle = style;
    }

    private void ApplyTextColumnAlignment(
        DataGridTextColumn column,
        HorizontalAlignment horizontalAlignment,
        VerticalAlignment verticalAlignment)
    {
        if (!_baseTextElementStyles.ContainsKey(column))
            _baseTextElementStyles[column] = column.ElementStyle;
        if (!_baseTextEditingStyles.ContainsKey(column))
            _baseTextEditingStyles[column] = column.EditingElementStyle;

        var textAlignment = ToTextAlignment(horizontalAlignment);

        var elementStyle = new Style(typeof(TextBlock), _baseTextElementStyles[column]);
        elementStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, horizontalAlignment));
        elementStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, verticalAlignment));
        elementStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, textAlignment));
        column.ElementStyle = elementStyle;

        var editingStyle = new Style(typeof(TextBox), _baseTextEditingStyles[column]);
        editingStyle.Setters.Add(new Setter(TextBox.TextAlignmentProperty, textAlignment));
        editingStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, horizontalAlignment));
        editingStyle.Setters.Add(new Setter(TextBox.VerticalContentAlignmentProperty, ToTextBoxVerticalAlignment(verticalAlignment)));
        column.EditingElementStyle = editingStyle;
    }

    private void AttachColumnLayoutChangeHandlers(DataGridColumn column)
    {
        if (!_trackedColumns.Add(column))
            return;

        DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn))
            ?.AddValueChanged(column, ColumnLayoutPropertyChanged);
        DependencyPropertyDescriptor.FromProperty(DataGridColumn.DisplayIndexProperty, typeof(DataGridColumn))
            ?.AddValueChanged(column, ColumnLayoutPropertyChanged);
    }

    private void DetachColumnLayoutChangeHandlers(DataGridColumn column)
    {
        DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn))
            ?.RemoveValueChanged(column, ColumnLayoutPropertyChanged);
        DependencyPropertyDescriptor.FromProperty(DataGridColumn.DisplayIndexProperty, typeof(DataGridColumn))
            ?.RemoveValueChanged(column, ColumnLayoutPropertyChanged);
    }

    private void ColumnLayoutPropertyChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        NotifyLayoutChanged();
    }

    private void NotifyLayoutChanged()
    {
        if (_isRestoring)
            return;

        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private static DataGridColumn? FindColumnByFieldName(IEnumerable<DataGridColumn> columns, string fieldName)
    {
        return columns.FirstOrDefault(column =>
            column.GetValue(FrameworkElement.TagProperty) is string tag &&
            string.Equals(tag, fieldName, StringComparison.OrdinalIgnoreCase));
    }

    private static TextAlignment ToTextAlignment(HorizontalAlignment alignment)
    {
        return alignment switch
        {
            HorizontalAlignment.Center => TextAlignment.Center,
            HorizontalAlignment.Right => TextAlignment.Right,
            _ => TextAlignment.Left
        };
    }

    private static VerticalAlignment ToTextBoxVerticalAlignment(VerticalAlignment alignment)
    {
        return alignment switch
        {
            VerticalAlignment.Top => VerticalAlignment.Top,
            VerticalAlignment.Bottom => VerticalAlignment.Bottom,
            _ => VerticalAlignment.Center
        };
    }

    private static HorizontalAlignment ParseHorizontalAlignment(string? value)
    {
        if (Enum.TryParse<HorizontalAlignment>(value, out var parsed))
            return parsed;
        return HorizontalAlignment.Left;
    }

    private static VerticalAlignment ParseVerticalAlignment(string? value)
    {
        if (Enum.TryParse<VerticalAlignment>(value, out var parsed))
            return parsed;
        return VerticalAlignment.Center;
    }
}
