using System.Runtime.ExceptionServices;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Sperre fuer Schacht-PDF-Vorgaenge (Einzel- und Ordnerimport, Neueinlesen, Stammdaten-Nachlauf): ein gemeinsamer
/// Zustand je Shell und der Guard, der Navigation, Projektwechsel und Speichern waehrenddessen sperrt.
/// Unveraendert aus <see cref="SchaechtePageViewModel"/> herausgeloest (24.09.2026), damit die Seite unter der
/// Groessengrenze bleibt (MaintainabilityFitnessTests); vorher private eingebettete Klassen.
/// </summary>
internal static class ProtocolImportVerfuegbarkeit
{
    internal static void NotifyAll(
        EventHandler? handlers,
        object sender,
        string secondaryErrorDataKey)
    {
        Exception? firstError = null;
        var secondaryErrorIndex = 0;
        foreach (var callback in handlers?.GetInvocationList() ?? [])
        {
            try
            {
                ((EventHandler)callback)(sender, EventArgs.Empty);
            }
            catch (Exception notificationError)
            {
                if (firstError is null)
                {
                    firstError = notificationError;
                }
                else
                {
                    firstError.Data[$"{secondaryErrorDataKey}.{secondaryErrorIndex++}"] =
                        notificationError;
                }
            }
        }

        if (firstError is not null)
            ExceptionDispatchInfo.Capture(firstError).Throw();
    }
}

internal sealed class SharedProtocolImportOperationState
{
    private readonly object _gate = new();
    private ProtocolImportShellOperationGuard? _owner;

    internal event EventHandler? AvailabilityChanged;

    internal bool IsActive
    {
        get
        {
            lock (_gate)
                return _owner is not null;
        }
    }

    internal bool IsOwnedBy(ProtocolImportShellOperationGuard guard)
    {
        lock (_gate)
            return ReferenceEquals(_owner, guard);
    }

    internal bool TryAcquire(ProtocolImportShellOperationGuard guard)
    {
        ArgumentNullException.ThrowIfNull(guard);
        lock (_gate)
        {
            if (_owner is not null)
                return false;

            _owner = guard;
        }

        try
        {
            ProtocolImportVerfuegbarkeit.NotifyAll(
                AvailabilityChanged,
                this,
                "ProtocolPdfAvailabilityNotificationError");
            return true;
        }
        catch (Exception notificationError)
        {
            var rolledBack = false;
            lock (_gate)
            {
                if (ReferenceEquals(_owner, guard))
                {
                    _owner = null;
                    rolledBack = true;
                }
            }

            if (rolledBack)
            {
                try
                {
                    ProtocolImportVerfuegbarkeit.NotifyAll(
                        AvailabilityChanged,
                        this,
                        "ProtocolPdfRollbackNotificationError");
                }
                catch (Exception rollbackNotificationError)
                {
                    notificationError.Data[
                        "ProtocolImportGuardRollbackNotificationError"] =
                        rollbackNotificationError;
                }
            }

            throw;
        }
    }

    internal void Release(ProtocolImportShellOperationGuard guard)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_owner, guard))
                return;

            _owner = null;
        }

        ProtocolImportVerfuegbarkeit.NotifyAll(
            AvailabilityChanged,
            this,
            "ProtocolPdfReleaseNotificationError");
    }
}

internal sealed class ProtocolImportShellOperationGuard : IShellOperationGuard, IDisposable
{
    private readonly SharedProtocolImportOperationState _state;
    private bool _disposed;

    internal ProtocolImportShellOperationGuard(SharedProtocolImportOperationState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _state.AvailabilityChanged += OnAvailabilityChanged;
    }

    public bool CanSaveProjectFromShell => !_state.IsActive;

    public string ProjectSaveBlockedMessage
        => "Manuelles Speichern ist waehrend einer Schacht-PDF-Verarbeitung gesperrt. " +
           "Bitte den laufenden Vorgang zuerst abschliessen.";

    public bool AllowsInternalProjectSave => _state.IsOwnedBy(this);

    public bool CanLeaveShellContext => !_state.IsActive;

    public string LeaveBlockedMessage
        => "Navigation, Projektwechsel und Schliessen sind waehrend des " +
           "Schacht-PDF-Vorgangs gesperrt. Bitte den laufenden Vorgang zuerst abschliessen.";

    public event EventHandler? OperationAvailabilityChanged;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _state.AvailabilityChanged -= OnAvailabilityChanged;
    }

    private void OnAvailabilityChanged(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        ProtocolImportVerfuegbarkeit.NotifyAll(
            OperationAvailabilityChanged,
            this,
            "ProtocolPdfGuardNotificationError");
    }
}
