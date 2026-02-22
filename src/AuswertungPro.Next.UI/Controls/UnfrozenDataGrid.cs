using System;
using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// DataGrid variant that disallows frozen columns.
/// </summary>
public class UnfrozenDataGrid : DataGrid
{
    static UnfrozenDataGrid()
    {
        FrozenColumnCountProperty.OverrideMetadata(
            typeof(UnfrozenDataGrid),
            new FrameworkPropertyMetadata(
                0,
                null,
                CoerceFrozenColumnCount));
    }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        SetCurrentValue(FrozenColumnCountProperty, 0);
    }

    private static object CoerceFrozenColumnCount(DependencyObject d, object baseValue)
    {
        _ = d;
        _ = baseValue;
        return 0;
    }
}
