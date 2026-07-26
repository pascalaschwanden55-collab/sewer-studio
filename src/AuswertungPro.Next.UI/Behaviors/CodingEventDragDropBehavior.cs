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
///
/// Session-Schutz: Der Drag-Zustand liegt NICHT mehr in statischen Feldern, sondern reist im
/// Drag-Payload (<see cref="CodingEventDragPayload"/>) mit. Quelle und Ziel muessen denselben
/// <c>SessionKey</c> tragen (pro PlayerWindow gesetzt, denn ein Fenster = genau eine
/// Haltung/Session) — sonst wird der Drop verworfen (Effects = None) und einmal pro Zug der
/// <c>ForeignDropHint</c> gezeigt. So kann ein Drag aus einem zweiten, nicht-modalen
/// PlayerWindow nicht mehr ungeprueft in der Session des Zielfensters landen.
/// </summary>
public static class CodingEventDragDropBehavior
{
    private const string Format = "SewerStudio.CodingEvent";

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

    // ── SessionKey: Identitaet der Haltung/Session dieser Liste (per Code-behind gesetzt).
    //    Drops werden nur akzeptiert, wenn Quell- und Ziel-Key uebereinstimmen. ──
    public static readonly DependencyProperty SessionKeyProperty = DependencyProperty.RegisterAttached(
        "SessionKey", typeof(object), typeof(CodingEventDragDropBehavior), new PropertyMetadata(null));
    public static void SetSessionKey(DependencyObject d, object? v) => d.SetValue(SessionKeyProperty, v);
    public static object? GetSessionKey(DependencyObject d) => d.GetValue(SessionKeyProperty);

    // ── ForeignDropHint: Einmaliger Hinweis pro Zug, wenn ein fremder Drop abgelehnt wird. ──
    public static readonly DependencyProperty ForeignDropHintProperty = DependencyProperty.RegisterAttached(
        "ForeignDropHint", typeof(Action), typeof(CodingEventDragDropBehavior),
        new PropertyMetadata(null));
    public static void SetForeignDropHint(DependencyObject d, Action? v) => d.SetValue(ForeignDropHintProperty, v);
    public static Action? GetForeignDropHint(DependencyObject d)
        => (Action?)d.GetValue(ForeignDropHintProperty);

    // ── DropHandler: (droppedEvent, targetIsKi) — per Code-behind gesetzt. Die Richtung (targetIsKi)
    //    bestimmt die Aktion; ein Kopie/Verschieben-Flag gibt es bewusst nicht mehr. ──
    public static readonly DependencyProperty DropHandlerProperty = DependencyProperty.RegisterAttached(
        "DropHandler", typeof(Action<CodingEvent, bool>), typeof(CodingEventDragDropBehavior),
        new PropertyMetadata(null));
    public static void SetDropHandler(DependencyObject d, Action<CodingEvent, bool>? v) => d.SetValue(DropHandlerProperty, v);
    public static Action<CodingEvent, bool>? GetDropHandler(DependencyObject d)
        => (Action<CodingEvent, bool>?)d.GetValue(DropHandlerProperty);

    // ── DragStart: Maus-Startpunkt pro Liste (Instanz-State statt frueherem statischem Feld) ──
    private static readonly DependencyProperty DragStartProperty = DependencyProperty.RegisterAttached(
        "DragStart", typeof(Point), typeof(CodingEventDragDropBehavior), new PropertyMetadata(default(Point)));
    private static void SetDragStart(DependencyObject d, Point v) => d.SetValue(DragStartProperty, v);
    private static Point GetDragStart(DependencyObject d) => (Point)d.GetValue(DragStartProperty);

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
        if (sender is ListBox list)
            SetDragStart(list, e.GetPosition(null));
    }

    private static void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not ListBox list)
            return;

        var pos = e.GetPosition(null);
        var dragStart = GetDragStart(list);
        if (Math.Abs(pos.X - dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        if (ItemFromPoint(list, e.GetPosition(list)) is not CodingEvent ev)
            return;

        var payload = new CodingEventDragPayload(ev, list, GetSessionKey(list));
        DragDrop.DoDragDrop(list, CreateDragData(payload), DragDropEffects.Move | DragDropEffects.Copy);
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        if (sender is not ListBox target)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        // Ueber der Herkunftsspalte und bei fremder Haltung/Session kein "Drop-OK"-Cursor.
        // Sonst zeigt die Richtung die Aktion: Ziel = KI (Import→KI) = Kopieren, sonst (KI→Import) = Verschieben.
        var payload = TryReadPayload(e.Data);
        e.Effects = ResolveDropEffects(payload, target);
        if (e.Effects == DragDropEffects.None
            && payload is not null
            && !ReferenceEquals(payload.SourceList, target)
            && !IsSameSession(payload.SourceSessionKey, GetSessionKey(target))
            && !payload.ForeignHintShown)
        {
            payload.ForeignHintShown = true;
            GetForeignDropHint(target)?.Invoke();
        }
        e.Handled = true;
    }

    private static void OnDrop(object sender, DragEventArgs e)
    {
        if (sender is not ListBox targetList)
            return;

        var payload = TryReadPayload(e.Data);
        if (payload?.Event is null)
            return;
        if (ResolveDropEffects(payload, targetList) == DragDropEffects.None)
            return; // gleiche Spalte oder fremde Haltung/Session -> nichts tun

        var targetIsKi = GetIsKiColumn(targetList);
        GetDropHandler(targetList)?.Invoke(payload.Event, targetIsKi);
        e.Handled = true;
    }

    /// <summary>
    /// Zentrale Drop-Pruefung: akzeptiert wird nur ein gueltiger Payload aus einer ANDEREN
    /// Spalte derselben Haltung/Session (gleicher <c>SessionKey</c>). Rueckgabe: der passende
    /// Drop-Effekt oder <see cref="DragDropEffects.None"/> zum Verwerfen.
    /// </summary>
    internal static DragDropEffects ResolveDropEffects(CodingEventDragPayload? payload, ListBox target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (payload?.Event is null)
            return DragDropEffects.None;
        if (ReferenceEquals(payload.SourceList, target))
            return DragDropEffects.None; // Drop in dieselbe Spalte -> nichts tun
        if (!IsSameSession(payload.SourceSessionKey, GetSessionKey(target)))
            return DragDropEffects.None; // fremde Haltung/Session (z.B. anderes PlayerWindow) -> sperren
        return GetIsKiColumn(target) ? DragDropEffects.Copy : DragDropEffects.Move;
    }

    /// <summary>Gleiche Haltung/Session = beide Keys gesetzt und gleich.</summary>
    internal static bool IsSameSession(object? sourceKey, object? targetKey)
        => sourceKey is not null && targetKey is not null && Equals(sourceKey, targetKey);

    internal static DataObject CreateDragData(CodingEventDragPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return new DataObject(Format, payload);
    }

    internal static CodingEventDragPayload? TryReadPayload(IDataObject? data)
        => data?.GetData(Format) as CodingEventDragPayload;

    private static object? ItemFromPoint(ListBox list, Point p)
        => ResolveItemData(list.InputHitTest(p) as DependencyObject);

    /// <summary>Gibt den Datenkontext des umschliessenden Listeneintrags zurueck.</summary>
    internal static object? ResolveItemData(DependencyObject? source)
        => VisualTreeSafe.FindAncestor<ListBoxItem>(source)?.DataContext;
}

/// <summary>
/// Drag-Payload einer Befund-Kachel: das Event selbst plus Herkunft (Quell-Spalte und
/// Session-Key). Da der Payload mit dem Drag reist, funktioniert die Pruefung auch
/// fensteruebergreifend — ganz ohne statische, zwischen Fenstern geteilte Felder.
/// </summary>
internal sealed class CodingEventDragPayload
{
    internal CodingEventDragPayload(CodingEvent codingEvent, ListBox sourceList, object? sourceSessionKey)
    {
        Event = codingEvent ?? throw new ArgumentNullException(nameof(codingEvent));
        SourceList = sourceList ?? throw new ArgumentNullException(nameof(sourceList));
        SourceSessionKey = sourceSessionKey;
    }

    internal CodingEvent Event { get; }

    internal ListBox SourceList { get; }

    internal object? SourceSessionKey { get; }

    internal bool ForeignHintShown { get; set; }
}
