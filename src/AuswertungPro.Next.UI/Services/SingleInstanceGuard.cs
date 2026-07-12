using System;
using System.Threading;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Verhindert, dass zwei SewerStudio-Instanzen gleichzeitig dieselben Daten schreiben.
/// </summary>
internal sealed class SingleInstanceGuard : IDisposable
{
    internal const string DefaultMutexName = @"Local\SewerStudio.SingleInstance";

    private readonly string _mutexName;
    private Mutex? _mutex;
    private bool _ownsMutex;

    public SingleInstanceGuard(string? mutexName = null)
    {
        _mutexName = string.IsNullOrWhiteSpace(mutexName) ? DefaultMutexName : mutexName;
    }

    public bool TryAcquire()
    {
        if (_ownsMutex)
            return true;

        _mutex = new Mutex(initiallyOwned: true, _mutexName, out var createdNew);
        if (createdNew)
        {
            _ownsMutex = true;
            return true;
        }

        try
        {
            _ownsMutex = _mutex.WaitOne(TimeSpan.Zero);
            return _ownsMutex;
        }
        catch (AbandonedMutexException)
        {
            // Die vorherige Instanz ist abgestuerzt. Der Mutex gehoert jetzt dieser Instanz.
            _ownsMutex = true;
            return true;
        }
    }

    public void Dispose()
    {
        if (_ownsMutex)
        {
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Best effort beim Herunterfahren.
            }
        }

        _ownsMutex = false;
        _mutex?.Dispose();
        _mutex = null;
    }
}
