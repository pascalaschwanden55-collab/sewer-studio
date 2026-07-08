using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Behaviors;

/// <summary>
/// Drag&amp;Drop von Befund-Kacheln (<see cref="CodingEvent"/>) zwischen den zwei Spalten des
/// Abgleich-Panels (KI-Befunde links ↔ Import rechts). Die RICHTUNG bestimmt die Aktion:
/// Import → KI (rechts → links) = Kopieren (Import bleibt erhalten); KI → Import (links → rechts)
/// = Verschieben und eingliedern. Die Strg-Taste spielt keine Rolle.
///
/// Das Behavior erkennt NUR die Richtung (Ziel = KI-Spalte?) und ruft einen Callback
/// (<c>DropHandler</c>). Die eigentliche Daten-/Session-Logik (Collections + der
/// <c>CodingSessionService</c>) liegt im Code-behind — denn „in die KI-Spalte" heisst ein
/// echter, noch unbestaetigter Session-Befund, „aus der KI heraus" ein sauberes Entfernen aus
/// der Session, und die Import-Spalte ist reine UI-Referenz.
/// </summary>
public static class CodingEventDragDropBehavior
{
    private const string Format = "SewerStudio.CodingEvent";
    private static Point _dragStart;
    private static ListBox? _sourceList;

    // ── Enabled (in XAML setzbar) ──
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(CodingEventDragDropBehavior),
        new PropertyMetadata(false, OnEnabledChanged));
    public static void SetEnabled(DependencyObject d, bool v) => d.SetValue(EnabledProperty, v);
    public static bool GetEnabled(DependencyObject d) => (bool)d.GetValue(EnabledProperty);

    // ── IsKiColumn: Ist diese Liste die KI-Spalte? (per Code-behind gesetzt) ──
    public static readonly DependencyProperty IsKiColumnProperty = DependencyProperty.RegisterAttached(
        "IsKiColumn", typeof(bool), typeof(CodingEventDragDropBehavior), new PropertyMetadata(false));
    public static void SetIsKiColumn(DependencyObject d, bool v) => d.SetValue(IsKiColumnProperty, v);
    public static bool GetIsKiColumn(DependencyObject d) => (bool)d.GetValue(IsKiColumnProperty);

    // ── DropHandler: (droppedEvent, targetIsKi) — per Code-behind gesetzt. Die Richtung (targetIsKi)
    //    bestimmt die Aktion; ein Kopie/Verschieben-Flag gibt es bewusst nicht mehr. ──
    public static readonly DependencyProperty DropHandlerProperty = DependencyProperty.RegisterAttached(
        "DropHandler", typeof(Action<CodingEvent, bool>), typeof(CodingEventDragDropBehavior),
        new PropertyMetadata(null));
    public static void SetDropHandler(DependencyObject d, Action<CodingEvent, bool>? v) => d.SetValue(DropHandlerProperty, v);
    public static Action<CodingEvent, bool>? GetDropHandler(DependencyObject d)
        => (Action<CodingEvent, bool>?)d.GetValue(DropHandlerProperty);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox list) return;
        if ((bool)e.NewValue)
        {
            list.PreviewMouseLeftButtonDown += OnMouseDown;
            list.PreviewMouseMove += OnMouseMove;
            list.AllowDrop = true;
            list.DragOver += OnDragOver;
            list.Drop += OnDrop;
        }
        else
        {
            list.PreviewMouseLeftButtonDown -= OnMouseDown;
            list.PreviewMouseMove -= OnMouseMove;
            list.DragOver -= OnDragOver;
            list.Drop -= OnDrop;
        }
    }

    private static void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _sourceList = sender as ListBox;
    }

    private static void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not ListBox list)
            return;

        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        if (ItemFromPoint(list, e.GetPosition(list)) is not CodingEvent ev)
            return;

        _sourceList = list;
        DragDrop.DoDragDrop(list, new DataObject(Format, ev), DragDropEffects.Move | DragDropEffects.Copy);
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        // Ueber der Herkunftsspalte kein "Drop-OK"-Cursor (dort ist der Drop ohnehin ein No-Op).
        // Sonst zeigt die Richtung die Aktion: Ziel = KI (Import→KI) = Kopieren, sonst (KI→Import) = Verschieben.
        var overSource = ReferenceEquals(_sourceList, sender);
        if (overSource || !e.Data.GetDataPresent(Format) || sender is not ListBox target)
            e.Effects = DragDropEffects.None;
        else
            e.Effects = GetIsKiColumn(target) ? DragDropEffects.Copy : DragDropEffects.Move;
        e.Handled = true;
    }

    private static void OnDrop(object sender, DragEventArgs e)
    {
        if (sender is not ListBox targetList)
            return;
        if (e.Data.GetData(Format) is not CodingEvent ev)
            return;
        if (ReferenceEquals(_sourceList, targetList))
            return; // Drop in dieselbe Spalte -> nichts tun

        var targetIsKi = GetIsKiColumn(targetList);
        GetDropHandler(targetList)?.Invoke(ev, targetIsKi);
        e.Handled = true;
    }

    private static object? ItemFromPoint(ListBox list, Point p)
        => ResolveItemData(list.InputHitTest(p) as DependencyObject);

    /// <summary>
    /// Laeuft vom getroffenen Element bis zum umschliessenden <see cref="ListBoxItem"/> hoch und
    /// gibt dessen DataContext zurueck. WICHTIG: <see cref="UIElement.InputHitTest(Point)"/> kann ein
    /// <see cref="System.Windows.ContentElement"/> liefern (z.B. der Text-<c>Run</c> in der Kachel);
    /// <see cref="VisualTreeHelper.GetParent"/> kennt aber nur Visual/Visual3D und wuerfe sonst
    /// „... ist kein Visual oder Visual3D". Darum ueber ContentElemente per LogicalTree hochspringen,
    /// bis wieder ein Visual erreicht ist, und erst dann im VisualTree weiter.
    /// </summary>
    internal static object? ResolveItemData(DependencyObject? d)
    {
        while (d != null && d is not ListBoxItem)
        {
            d = d is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(d)
                : LogicalTreeHelper.GetParent(d);
        }
        return (d as ListBoxItem)?.DataContext;
    }
}
