using AuswertungPro.Next.UI;

namespace AuswertungPro.Next.UI.DataPage;

public sealed record DataPageGridLayoutState(
    double GridMinRowHeight,
    double GridZoom,
    bool IsColumnReorderEnabled);

public static class DataPageGridLayoutController
{
    public const double DefaultGridMinRowHeight = 38d;
    public const double MinGridMinRowHeight = 24d;
    public const double MaxGridMinRowHeight = 240d;
    public const double DefaultGridZoom = 1.0d;
    public const double MinGridZoom = 0.5d;
    public const double MaxGridZoom = 2.0d;

    public static DataPageGridLayoutState Restore(DataPageLayoutSettings? layout)
    {
        if (layout is null)
            return new DataPageGridLayoutState(DefaultGridMinRowHeight, DefaultGridZoom, false);

        return new DataPageGridLayoutState(
            IsValidGridMinRowHeight(layout.GridMinRowHeight)
                ? layout.GridMinRowHeight
                : DefaultGridMinRowHeight,
            IsValidGridZoom(layout.GridZoom)
                ? layout.GridZoom
                : DefaultGridZoom,
            layout.IsColumnReorderEnabled);
    }

    public static double ClampGridMinRowHeight(double value)
        => double.IsFinite(value)
            ? Math.Clamp(value, MinGridMinRowHeight, MaxGridMinRowHeight)
            : DefaultGridMinRowHeight;

    public static double ClampGridZoom(double value)
        => double.IsFinite(value)
            ? Math.Clamp(value, MinGridZoom, MaxGridZoom)
            : DefaultGridZoom;

    public static DataPageLayoutSettings Persist(
        DataPageLayoutSettings? layout,
        double gridMinRowHeight,
        double gridZoom,
        bool isColumnReorderEnabled,
        Action<DataPageLayoutSettings> setLayout,
        Action save)
    {
        ArgumentNullException.ThrowIfNull(setLayout);
        ArgumentNullException.ThrowIfNull(save);

        var updatedLayout = layout ?? new DataPageLayoutSettings();
        updatedLayout.GridMinRowHeight = ClampGridMinRowHeight(gridMinRowHeight);
        updatedLayout.GridZoom = ClampGridZoom(gridZoom);
        updatedLayout.IsColumnReorderEnabled = isColumnReorderEnabled;

        setLayout(updatedLayout);
        save();

        return updatedLayout;
    }

    private static bool IsValidGridMinRowHeight(double value)
        => double.IsFinite(value)
           && value is >= MinGridMinRowHeight and <= MaxGridMinRowHeight;

    private static bool IsValidGridZoom(double value)
        => double.IsFinite(value)
           && value is >= MinGridZoom and <= MaxGridZoom;
}
