using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.UI.Services;

/// <summary>Schweregrad eines Toasts (steuert Farbe und Anzeigedauer).</summary>
public enum ToastSeverity
{
    Success,
    Info,
    Warning,
    Error
}

/// <summary>Ein Toast: kurze, nicht-blockierende Statusmeldung unten rechts.</summary>
public sealed record ToastItem(long Id, string Message, ToastSeverity Severity)
{
    /// <summary>Anzeigedauer in ms; null = bleibt bis Klick (nur Error).</summary>
    public long? DurationMs => Severity switch
    {
        ToastSeverity.Warning => 5000,
        ToastSeverity.Error => null,
        _ => 3000, // Success / Info
    };
}

/// <summary>
/// UI-freie Warteschlangen-Logik fuer Toasts: max. 3 sichtbar, FIFO-Nachruecken,
/// Ablauf nach Schwere, Error bleibt bis Dismiss. Die Zeit wird als monotone
/// Millisekunden hereingereicht, damit die Logik ohne Timer/Uhr testbar bleibt.
/// </summary>
public sealed class ToastQueueLogic
{
    /// <summary>Maximale Anzahl gleichzeitig sichtbarer Toasts.</summary>
    public const int MaxVisible = 3;

    private readonly List<ActiveToast> _active = new();
    private readonly Queue<ToastItem> _pending = new();
    private long _nextId;

    private sealed record ActiveToast(ToastItem Item, long ShownAtMs);

    /// <summary>Aktuell sichtbare Toasts (aeltester zuerst).</summary>
    public IReadOnlyList<ToastItem> Visible => _active.Select(a => a.Item).ToList();

    /// <summary>Anzahl wartender (noch nicht sichtbarer) Toasts.</summary>
    public int PendingCount => _pending.Count;

    /// <summary>
    /// Reiht eine Meldung ein. Leere/Whitespace-Meldung wird verworfen (Rueckgabe null).
    /// Ist ein Slot frei, wird sie sofort sichtbar; sonst wandert sie in die Warteschlange.
    /// </summary>
    public long? Show(string message, ToastSeverity severity, long nowMs)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        var item = new ToastItem(++_nextId, message.Trim(), severity);
        if (_active.Count < MaxVisible)
            _active.Add(new ActiveToast(item, nowMs));
        else
            _pending.Enqueue(item);
        return item.Id;
    }

    /// <summary>Entfernt abgelaufene Toasts und rueckt Wartende nach. Vom Host periodisch aufgerufen.</summary>
    public void Prune(long nowMs)
    {
        _active.RemoveAll(a => a.Item.DurationMs is { } d && nowMs - a.ShownAtMs >= d);
        Promote(nowMs);
    }

    /// <summary>Entfernt einen Toast per Id (Klick) und rueckt Wartende nach.</summary>
    public void Dismiss(long id, long nowMs)
    {
        _active.RemoveAll(a => a.Item.Id == id);

        if (_pending.Any(p => p.Id == id))
        {
            var kept = _pending.Where(p => p.Id != id).ToList();
            _pending.Clear();
            foreach (var p in kept)
                _pending.Enqueue(p);
        }

        Promote(nowMs);
    }

    private void Promote(long nowMs)
    {
        while (_active.Count < MaxVisible && _pending.Count > 0)
            _active.Add(new ActiveToast(_pending.Dequeue(), nowMs));
    }
}
