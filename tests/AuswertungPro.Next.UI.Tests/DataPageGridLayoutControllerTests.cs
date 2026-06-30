using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageGridLayoutControllerTests
{
    [Fact]
    public void Restore_uses_saved_valid_grid_layout_state()
    {
        var layout = new DataPageLayoutSettings
        {
            GridMinRowHeight = 72d,
            GridZoom = 1.25d,
            IsColumnReorderEnabled = true
        };

        var state = DataPageGridLayoutController.Restore(layout);

        Assert.Equal(72d, state.GridMinRowHeight);
        Assert.Equal(1.25d, state.GridZoom);
        Assert.True(state.IsColumnReorderEnabled);
    }

    [Fact]
    public void Restore_falls_back_to_defaults_for_invalid_saved_numbers()
    {
        var state = DataPageGridLayoutController.Restore(new DataPageLayoutSettings
        {
            GridMinRowHeight = 241d,
            GridZoom = 0.49d,
            IsColumnReorderEnabled = true
        });

        Assert.Equal(38d, state.GridMinRowHeight);
        Assert.Equal(1.0d, state.GridZoom);
        Assert.True(state.IsColumnReorderEnabled);
    }

    [Fact]
    public void Restore_falls_back_to_defaults_for_missing_layout()
    {
        var state = DataPageGridLayoutController.Restore(null);

        Assert.Equal(38d, state.GridMinRowHeight);
        Assert.Equal(1.0d, state.GridZoom);
        Assert.False(state.IsColumnReorderEnabled);
    }

    [Theory]
    [InlineData(12d, 24d)]
    [InlineData(24d, 24d)]
    [InlineData(120d, 120d)]
    [InlineData(240d, 240d)]
    [InlineData(300d, 240d)]
    public void ClampGridMinRowHeight_limits_user_values(double value, double expected)
    {
        var clamped = DataPageGridLayoutController.ClampGridMinRowHeight(value);

        Assert.Equal(expected, clamped);
    }

    [Theory]
    [InlineData(0.25d, 0.5d)]
    [InlineData(0.5d, 0.5d)]
    [InlineData(1.25d, 1.25d)]
    [InlineData(2.0d, 2.0d)]
    [InlineData(3.0d, 2.0d)]
    public void ClampGridZoom_limits_user_values(double value, double expected)
    {
        var clamped = DataPageGridLayoutController.ClampGridZoom(value);

        Assert.Equal(expected, clamped);
    }

    [Fact]
    public void Persist_sets_grid_layout_fields_only_and_saves_once()
    {
        var columns = new List<DataPageColumnLayout>
        {
            new() { FieldName = "Haltung", DisplayIndex = 2 }
        };
        var layout = new DataPageLayoutSettings
        {
            GridMinRowHeight = 38d,
            GridZoom = 1.0d,
            IsColumnReorderEnabled = false,
            Columns = columns
        };
        var saveCount = 0;
        DataPageLayoutSettings? assignedLayout = null;

        var persisted = DataPageGridLayoutController.Persist(
            layout,
            gridMinRowHeight: 12d,
            gridZoom: 3.0d,
            isColumnReorderEnabled: true,
            setLayout: value => assignedLayout = value,
            save: () => saveCount++);

        Assert.Same(layout, persisted);
        Assert.Same(layout, assignedLayout);
        Assert.Equal(24d, layout.GridMinRowHeight);
        Assert.Equal(2.0d, layout.GridZoom);
        Assert.True(layout.IsColumnReorderEnabled);
        Assert.Same(columns, layout.Columns);
        Assert.Equal("Haltung", Assert.Single(layout.Columns).FieldName);
        Assert.Equal(1, saveCount);
    }
}
