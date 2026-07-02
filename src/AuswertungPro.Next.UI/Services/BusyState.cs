using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// UI-freie Busy-Anzeige-Logik fuer das <see cref="AuswertungPro.Next.UI.Controls.BusyOverlay"/>.
/// Zaehlerbasiert, damit sich verschachtelte Langlaeufer nicht gegenseitig ausblenden:
/// <c>using (Busy.Enter("...")) { ... }</c>. Die Meldung faellt beim Verlassen auf den
/// umgebenden Vorgang zurueck. Dispose ist idempotent und reihenfolge-unabhaengig.
/// </summary>
public sealed partial class BusyState : ObservableObject
{
    private readonly List<Entry> _entries = new();
    private long _nextId;

    private sealed record Entry(long Id, string Message);

    /// <summary>True, solange mindestens ein Vorgang laeuft.</summary>
    [ObservableProperty] private bool _isActive;

    /// <summary>Meldung des innersten laufenden Vorgangs (leer, wenn nichts laeuft).</summary>
    [ObservableProperty] private string _message = "";

    /// <summary>
    /// Startet einen Busy-Vorgang und liefert einen Scope, dessen Dispose ihn wieder beendet.
    /// </summary>
    public IDisposable Enter(string message)
    {
        var entry = new Entry(++_nextId, message ?? "");
        _entries.Add(entry);
        Refresh();
        return new Scope(this, entry.Id);
    }

    private void Leave(long id)
    {
        var index = _entries.FindIndex(e => e.Id == id);
        if (index < 0)
            return; // schon entfernt -> idempotent

        _entries.RemoveAt(index);
        Refresh();
    }

    private void Refresh()
    {
        IsActive = _entries.Count > 0;
        Message = _entries.Count > 0 ? _entries[^1].Message : "";
    }

    private sealed class Scope : IDisposable
    {
        private BusyState? _owner;
        private readonly long _id;

        public Scope(BusyState owner, long id)
        {
            _owner = owner;
            _id = id;
        }

        public void Dispose()
        {
            var owner = _owner;
            _owner = null; // zweiter Dispose ist ein No-Op
            owner?.Leave(_id);
        }
    }
}
