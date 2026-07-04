using System.ComponentModel;
using System.Runtime.CompilerServices;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Behaviors;

/// <summary>
/// Bindbarer Halter fuer Zeilenhoehe und Zoom eines personalisierbaren Grids (Dim3 Schrift/Dichte).
/// Werte werden beim Setzen auf die gueltigen Bereiche geklammert
/// (<see cref="DataPageGridLayoutController"/>). Persistenz teilt sich denselben Store-Slot
/// wie die Spalten (P1) — Zoom/Hoehe und Spalten liegen in EINEM DataPageLayoutSettings je Grid.
/// </summary>
public sealed class GridViewOptions : INotifyPropertyChanged
{
    private double _gridMinRowHeight = DataPageGridLayoutController.DefaultGridMinRowHeight;
    private double _gridZoom = DataPageGridLayoutController.DefaultGridZoom;

    public event PropertyChangedEventHandler? PropertyChanged;

    public double GridMinRowHeight
    {
        get => _gridMinRowHeight;
        set => SetClamped(ref _gridMinRowHeight, DataPageGridLayoutController.ClampGridMinRowHeight(value));
    }

    public double GridZoom
    {
        get => _gridZoom;
        set => SetClamped(ref _gridZoom, DataPageGridLayoutController.ClampGridZoom(value));
    }

    private void SetClamped(ref double field, double clamped, [CallerMemberName] string? property = null)
    {
        if (field.Equals(clamped))
            return;
        field = clamped;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }
}

/// <summary>
/// Reine, testbare Kernlogik fuer Seed/Persist der Grid-Ansichtsoptionen (ohne Loaded-Zyklus).
/// </summary>
internal static class GridViewOptionsCore
{
    /// <summary>Laedt Zeilenhoehe/Zoom aus dem Store-Slot in den Halter (geklammert).</summary>
    public static void Seed(GridViewOptions options, string viewKey, string gridKey)
    {
        var slot = ViewCustomizationStore.GetOrCreateGrid(viewKey, gridKey);
        var state = DataPageGridLayoutController.Restore(slot);
        options.GridMinRowHeight = state.GridMinRowHeight;
        options.GridZoom = state.GridZoom;
    }

    /// <summary>Schreibt Zeilenhoehe/Zoom in den Store-Slot (Spalten bleiben unberuehrt) und speichert.</summary>
    public static void Persist(GridViewOptions options, string viewKey, string gridKey)
    {
        var slot = ViewCustomizationStore.GetOrCreateGrid(viewKey, gridKey);
        slot.GridMinRowHeight = DataPageGridLayoutController.ClampGridMinRowHeight(options.GridMinRowHeight);
        slot.GridZoom = DataPageGridLayoutController.ClampGridZoom(options.GridZoom);
        ViewCustomizationStore.Save();
    }
}
