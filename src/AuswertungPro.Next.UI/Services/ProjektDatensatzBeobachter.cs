using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Services;

/// <summary>Meldet Aenderungen beider Listen und ihrer Datensaetze; meldet alle Abos wieder ab.</summary>
internal sealed class ProjektDatensatzBeobachter : IDisposable
{
    private readonly Project _projekt;
    private readonly Action _geaendert;
    private readonly HashSet<INotifyPropertyChanged> _records = [];
    private bool _disposed;
    private int _ausstehend;
    public ProjektDatensatzBeobachter(Project projekt, Action geaendert)
    {
        _projekt = projekt; _geaendert = geaendert;
        projekt.Data.CollectionChanged += Liste; projekt.SchaechteData.CollectionChanged += Liste;
        Verbinde();
    }
    private void Verbinde()
    {
        foreach (var r in _records) r.PropertyChanged -= Feld;
        _records.Clear();
        foreach (var r in _projekt.Data.Cast<INotifyPropertyChanged>().Concat(_projekt.SchaechteData))
            if (_records.Add(r)) r.PropertyChanged += Feld;
    }
    private void Liste(object? sender, NotifyCollectionChangedEventArgs e) { Verbinde(); Melde(); }
    private void Feld(object? sender, PropertyChangedEventArgs e) => Melde();
    private void Melde()
    {
        void Ausfuehren() { if (!_disposed) _geaendert(); }
        var d = System.Windows.Application.Current?.Dispatcher;
        if (d is null) { Ausfuehren(); return; }
        // Ein Import meldet viele Felder derselben Zeile. Nur einmal pro UI-Runde rechnen.
        if (Interlocked.Exchange(ref _ausstehend, 1) != 0) return;
        d.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
        {
            Interlocked.Exchange(ref _ausstehend, 0);
            Ausfuehren();
        });
    }
    public void Dispose()
    {
        _disposed = true;
        _projekt.Data.CollectionChanged -= Liste; _projekt.SchaechteData.CollectionChanged -= Liste;
        foreach (var r in _records) r.PropertyChanged -= Feld;
        _records.Clear();
    }
}
