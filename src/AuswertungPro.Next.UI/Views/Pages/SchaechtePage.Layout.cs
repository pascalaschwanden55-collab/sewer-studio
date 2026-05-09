using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Views.Pages;

// SchaechtePage Spalten-Layout-Persistierung — analog zu DataPage.Layout.cs.
// Plus Schacht-spezifische "Schachtnummer-vor-Funktion"-Sortierung.
// Aus dem Hauptdatei extrahiert (Slice 10b).
public partial class SchaechtePage
{
    private void RestoreLayoutFromSettings()
    {
        // Phase 5.1.B Etappe 3.F: via DI-Container.
        var layout = App.Resolve<AppSettings>().SchaechtePageLayout;
        if (layout is null)
            return;

        _isRestoringLayout = true;
        try
        {
            var byField = layout.Columns?
                .Where(c => !string.IsNullOrWhiteSpace(c.FieldName))
                .GroupBy(c => c.FieldName, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal)
                ?? new Dictionary<string, DataPageColumnLayout>(StringComparer.Ordinal);

            foreach (var col in Grid.Columns)
            {
                if (col.GetValue(FrameworkElement.TagProperty) is not string fieldName)
                    continue;
                if (!byField.TryGetValue(fieldName, out var state))
                    continue;

                if (state.WidthValue > 0 && Enum.TryParse<DataGridLengthUnitType>(state.WidthUnitType, out var widthType))
                    col.Width = new DataGridLength(state.WidthValue, widthType);

                var horizontal = ParseHorizontalAlignment(state.HorizontalAlignment);
                var vertical = ParseVerticalAlignment(state.VerticalAlignment);
                ApplyColumnAlignment(col, horizontal, vertical);
            }

            var orderedColumns = Grid.Columns
                .Select(col =>
                {
                    var field = col.GetValue(FrameworkElement.TagProperty) as string;
                    if (field is not null && byField.TryGetValue(field, out var state))
                        return new { Column = col, Target = state.DisplayIndex, HasState = true };
                    return new { Column = col, Target = col.DisplayIndex, HasState = false };
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
                    // ignore invalid display index operations
                }
            }

            EnsureSchachtnummerBeforeFunktion();
        }
        finally
        {
            _isRestoringLayout = false;
        }
    }

    private void EnsureSchachtnummerBeforeFunktion()
    {
        var schachtnummerColumn = FindColumnByName("Schachtnummer");
        var funktionColumn = FindColumnByName("Funktion");
        if (schachtnummerColumn is null || funktionColumn is null)
            return;

        if (schachtnummerColumn.DisplayIndex < funktionColumn.DisplayIndex)
            return;

        try
        {
            var target = funktionColumn.DisplayIndex;
            schachtnummerColumn.DisplayIndex = target;
            funktionColumn.DisplayIndex = target + 1;
        }
        catch
        {
            // ignore invalid display index operations
        }
    }

    private DataGridColumn? FindColumnByName(string fieldName)
    {
        return Grid.Columns.FirstOrDefault(c =>
            c.GetValue(FrameworkElement.TagProperty) is string tag &&
            string.Equals(tag, fieldName, StringComparison.OrdinalIgnoreCase));
    }

    private void AttachColumnLayoutChangeHandlers(DataGridColumn column)
    {
        DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn))
            ?.AddValueChanged(column, ColumnLayoutPropertyChanged);
        DependencyPropertyDescriptor.FromProperty(DataGridColumn.DisplayIndexProperty, typeof(DataGridColumn))
            ?.AddValueChanged(column, ColumnLayoutPropertyChanged);
    }

    private void ColumnLayoutPropertyChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        QueueLayoutSave();
    }

    private void QueueLayoutSave()
    {
        if (_isRestoringLayout)
            return;

        _layoutSaveDebounceTimer.Stop();
        _layoutSaveDebounceTimer.Start();
    }

    private void SaveLayoutToSettings()
    {
        if (_isRestoringLayout || Grid.Columns.Count == 0)
            return;

        // Phase 5.1.B Etappe 3.F: via DI-Container.
        var settings = App.Resolve<AppSettings>();
        var layout = settings.SchaechtePageLayout ?? new DataPageLayoutSettings();
        layout.Columns = Grid.Columns
            .Select(col =>
            {
                var fieldName = col.GetValue(FrameworkElement.TagProperty) as string ?? "";
                var horizontal = GetColumnHorizontalAlignment(col).ToString();
                var vertical = GetColumnVerticalAlignment(col).ToString();
                return new DataPageColumnLayout
                {
                    FieldName = fieldName,
                    DisplayIndex = col.DisplayIndex,
                    WidthValue = col.Width.Value,
                    WidthUnitType = col.Width.UnitType.ToString(),
                    HorizontalAlignment = horizontal,
                    VerticalAlignment = vertical
                };
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.FieldName))
            .ToList();

        settings.SchaechtePageLayout = layout;
        settings.Save();
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
