using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class SchaechtePageViewModel
{
    private static readonly ConditionalWeakTable<
        ShellViewModel,
        SharedProtocolImportOperationState> SharedProtocolImportStates = new();

    private readonly object _protocolImportLifecycleGate = new();
    private readonly SharedProtocolImportOperationState _sharedProtocolImportState;
    private readonly ProtocolImportShellOperationGuard _protocolImportShellGuard;
    private readonly Func<bool> _saveProjectForProtocolImport;
    private bool _protocolImportDisposeRequested;
    private bool _protocolImportGuardUnregistered;
    private bool _protocolPdfOperationLifecycleActive;

    public bool CanMutateShaftData
    {
        get
        {
            lock (_protocolImportLifecycleGate)
            {
                return !_protocolImportDisposeRequested
                       && !_protocolPdfOperationLifecycleActive
                       && !_sharedProtocolImportState.IsActive;
            }
        }
    }

    public bool IsShaftDataReadOnly => !CanMutateShaftData;

    public bool ConfirmLeave()
    {
        if (_protocolImportShellGuard.CanLeaveShellContext)
            return true;

        _shell.SetStatus(_protocolImportShellGuard.LeaveBlockedMessage);
        return false;
    }

    public void Dispose()
    {
        var unregisterNow = false;
        lock (_protocolImportLifecycleGate)
        {
            if (_protocolImportDisposeRequested)
                return;

            _protocolImportDisposeRequested = true;
            if (!_protocolPdfOperationLifecycleActive
                && !_sharedProtocolImportState.IsOwnedBy(_protocolImportShellGuard))
            {
                _protocolImportGuardUnregistered = true;
                unregisterNow = true;
            }
        }

        if (unregisterNow)
            UnregisterProtocolImportGuard();

        GC.SuppressFinalize(this);
    }

    private bool CanStartProtocolPdfOperation()
    {
        lock (_protocolImportLifecycleGate)
        {
            return !_protocolImportDisposeRequested
                   && !_protocolPdfOperationLifecycleActive
                   && !_sharedProtocolImportState.IsActive;
        }
    }

    internal bool CanMutateRecord(
        SchachtRecord? record,
        string operationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        if (!CanMutateShaftData)
        {
            ReportBlockedShaftDataMutation(
                $"{operationName} ist waehrend einer Schacht-PDF-Verarbeitung gesperrt.");
            return false;
        }

        if (record is null)
            return false;

        lock (_shell.CollectionLock)
        {
            if (_shell.Project.SchaechteData.Contains(record))
                return true;
        }

        ReportBlockedShaftDataMutation(
            $"{operationName} nicht ausgefuehrt: Der Schacht gehoert nicht mehr zum aktuellen Projekt.");
        return false;
    }

    private bool CanMutateShaftDataForCommand()
        => CanMutateShaftData;

    private bool EnsureShaftDataMutationAllowed(string operationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        if (CanMutateShaftData)
            return true;

        ReportBlockedShaftDataMutation(
            $"{operationName} ist waehrend einer Schacht-PDF-Verarbeitung gesperrt.");
        return false;
    }

    private void ReportBlockedShaftDataMutation(string message)
    {
        LastResult = message;
        _shell.SetStatus(message);
    }

    private bool TryBeginProtocolPdfOperation(string operationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        var disposed = false;
        var acquired = false;
        var unregisterNow = false;
        try
        {
            lock (_protocolImportLifecycleGate)
            {
                disposed = _protocolImportDisposeRequested;
                if (!disposed && !_protocolPdfOperationLifecycleActive)
                {
                    _protocolPdfOperationLifecycleActive = true;
                    var shellAcquired = false;
                    try
                    {
                        shellAcquired = _shell.TryAcquireProjectOperation(
                            _protocolImportShellGuard);
                        if (shellAcquired)
                        {
                            acquired = _sharedProtocolImportState.TryAcquire(
                                _protocolImportShellGuard);
                        }
                    }
                    finally
                    {
                        try
                        {
                            if (shellAcquired && !acquired)
                            {
                                _shell.ReleaseProjectOperation(
                                    _protocolImportShellGuard);
                            }
                        }
                        finally
                        {
                            if (!acquired)
                            {
                                unregisterNow =
                                    CompleteProtocolPdfOperationLifecycleUnderLock();
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            if (unregisterNow)
                UnregisterProtocolImportGuard();

            throw;
        }

        if (unregisterNow)
            UnregisterProtocolImportGuard();

        if (acquired)
            return true;

        LastResult = disposed
            ? $"{operationName} nicht gestartet: Die Schachtseite wurde geschlossen."
            : $"{operationName} nicht gestartet: Es laeuft bereits ein anderer Projektvorgang.";
        _shell.SetStatus(LastResult);
        return false;
    }

    private void EndProtocolPdfOperation()
    {
        try
        {
            _sharedProtocolImportState.Release(_protocolImportShellGuard);
        }
        finally
        {
            try
            {
                _shell.ReleaseProjectOperation(_protocolImportShellGuard);
            }
            finally
            {
                var unregisterNow = false;
                lock (_protocolImportLifecycleGate)
                {
                    unregisterNow =
                        CompleteProtocolPdfOperationLifecycleUnderLock();
                }

                if (unregisterNow)
                {
                    UnregisterProtocolImportGuard();
                }
                else
                {
                    NotifyProtocolPdfOperationCommands();
                }
            }
        }
    }

    private bool CompleteProtocolPdfOperationLifecycleUnderLock()
    {
        _protocolPdfOperationLifecycleActive = false;
        if (!_protocolImportDisposeRequested || _protocolImportGuardUnregistered)
            return false;

        _protocolImportGuardUnregistered = true;
        return true;
    }

    private static void NotifyAllOperationAvailabilityHandlers(
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

    private void UnregisterProtocolImportGuard()
    {
        _protocolImportShellGuard.OperationAvailabilityChanged -=
            OnProtocolPdfOperationAvailabilityChanged;
        try
        {
            _shell.UnregisterShellOperationGuard(_protocolImportShellGuard);
        }
        finally
        {
            _protocolImportShellGuard.Dispose();
        }
    }

    private void OnProtocolPdfOperationAvailabilityChanged(
        object? sender,
        EventArgs args)
    {
        _ = sender;
        _ = args;
        NotifyProtocolPdfOperationCommands();
    }

    private void NotifyProtocolPdfOperationCommands()
    {
        OnPropertyChanged(nameof(CanMutateShaftData));
        OnPropertyChanged(nameof(IsShaftDataReadOnly));
        AddCommand?.NotifyCanExecuteChanged();
        RemoveCommand?.NotifyCanExecuteChanged();
        MoveUpCommand?.NotifyCanExecuteChanged();
        MoveDownCommand?.NotifyCanExecuteChanged();
        SaveCommand?.NotifyCanExecuteChanged();
        ImportProtocolCommand?.NotifyCanExecuteChanged();
        RefreshProtocolCommand?.NotifyCanExecuteChanged();
        ErgaenzeStammdatenAusPdfsCommand?.NotifyCanExecuteChanged();
    }

    private sealed class SharedProtocolImportOperationState
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
                NotifyAllOperationAvailabilityHandlers(
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
                        NotifyAllOperationAvailabilityHandlers(
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

            NotifyAllOperationAvailabilityHandlers(
                AvailabilityChanged,
                this,
                "ProtocolPdfReleaseNotificationError");
        }
    }

    private sealed class ProtocolImportShellOperationGuard : IShellOperationGuard, IDisposable
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
            NotifyAllOperationAvailabilityHandlers(
                OperationAvailabilityChanged,
                this,
                "ProtocolPdfGuardNotificationError");
        }
    }
}
