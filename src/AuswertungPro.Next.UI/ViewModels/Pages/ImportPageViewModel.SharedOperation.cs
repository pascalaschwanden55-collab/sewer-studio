using System.Runtime.CompilerServices;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class ImportPageViewModel
{
    // Die Navigation erzeugt fuer dieselbe Shell neue Seiteninstanzen. Die schwache
    // Zuordnung teilt deren Sperre, ohne alte Shells oder Seiten im Speicher zu halten.
    private static readonly ConditionalWeakTable<ShellViewModel, SharedImportOperationState>
        SharedImportStates = new();

    private readonly SharedImportOperationState _sharedImportState;
    private readonly object _sharedImportStateApplyGate = new();
    private long _desiredSharedImportStateVersion = -1;
    private long _appliedSharedImportStateVersion = -1;
    private bool _desiredSharedImportState;
    private bool _isApplyingSharedImportState;

    private async Task RunWithSharedImportLockAsync(Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (IsImportInProgress
            || !_shell.TryAcquireProjectOperation(_sharedImportState))
        {
            _shell.SetStatus("Es laeuft bereits ein Import oder ein anderer Projektvorgang.");
            return;
        }

        var sharedImportAcquired = false;
        try
        {
            if (!_sharedImportState.TryAcquire())
            {
                _shell.SetStatus("Es laeuft bereits ein Import.");
                return;
            }

            sharedImportAcquired = true;
            await operation();
        }
        finally
        {
            try
            {
                if (sharedImportAcquired)
                    _sharedImportState.Release();
            }
            finally
            {
                _shell.ReleaseProjectOperation(_sharedImportState);
            }
        }
    }

    private void SetSharedImportInProgress(bool value, long version)
    {
        lock (_sharedImportStateApplyGate)
        {
            if (version < _desiredSharedImportStateVersion)
                return;

            if (version > _desiredSharedImportStateVersion)
            {
                _desiredSharedImportStateVersion = version;
                _desiredSharedImportState = value;
            }

            if (_isApplyingSharedImportState
                || _appliedSharedImportStateVersion >= _desiredSharedImportStateVersion)
            {
                return;
            }

            _isApplyingSharedImportState = true;
        }

        while (true)
        {
            bool nextValue;
            long nextVersion;
            lock (_sharedImportStateApplyGate)
            {
                nextValue = _desiredSharedImportState;
                nextVersion = _desiredSharedImportStateVersion;
            }

            try
            {
                // PropertyChanged kann fremden UI-Code ausloesen und darf deshalb
                // nie unter dem gemeinsamen Zustands-Lock laufen.
                IsImportInProgress = nextValue;
            }
            catch
            {
                lock (_sharedImportStateApplyGate)
                {
                    _isApplyingSharedImportState = false;
                }

                throw;
            }

            lock (_sharedImportStateApplyGate)
            {
                _appliedSharedImportStateVersion = Math.Max(
                    _appliedSharedImportStateVersion,
                    nextVersion);
                if (_appliedSharedImportStateVersion >= _desiredSharedImportStateVersion)
                {
                    _isApplyingSharedImportState = false;
                    return;
                }
            }
        }
    }

    private sealed class SharedImportOperationState : IShellOperationGuard
    {
        private readonly object _gate = new();
        private readonly List<WeakReference<ImportPageViewModel>> _viewModels = [];
        private bool _isActive;
        private long _version;

        public bool CanSaveProjectFromShell => !IsActive;

        public string ProjectSaveBlockedMessage
            => "Manuelles Speichern ist waehrend eines Imports gesperrt. " +
               "Bitte den Import zuerst abschliessen oder abbrechen.";

        public bool AllowsInternalProjectSave => IsActive;

        public bool CanLeaveShellContext => !IsActive;

        public string LeaveBlockedMessage
            => "Navigation, Projektwechsel und Schliessen sind waehrend eines Imports gesperrt. " +
               "Bitte den Import zuerst abschliessen oder abbrechen.";

        public event EventHandler? OperationAvailabilityChanged;

        public bool IsActive
        {
            get
            {
                lock (_gate)
                {
                    return _isActive;
                }
            }
        }

        public void Register(ImportPageViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            bool isActive;
            long version;
            lock (_gate)
            {
                var liveViewModels = CollectLiveViewModels();
                if (!liveViewModels.Any(existing => ReferenceEquals(existing, viewModel)))
                    _viewModels.Add(new WeakReference<ImportPageViewModel>(viewModel));
                isActive = _isActive;
                version = _version;
            }

            while (true)
            {
                viewModel.SetSharedImportInProgress(isActive, version);

                lock (_gate)
                {
                    if (version == _version)
                        return;

                    isActive = _isActive;
                    version = _version;
                }
            }
        }

        public bool TryAcquire()
        {
            List<ImportPageViewModel> liveViewModels;
            long version;
            lock (_gate)
            {
                if (_isActive)
                    return false;

                _isActive = true;
                version = ++_version;
                liveViewModels = CollectLiveViewModels();
            }

            try
            {
                PublishState(liveViewModels, value: true, version);
                return true;
            }
            catch (Exception publishError)
            {
                RollbackFailedAcquire(version, publishError);
                throw;
            }
        }

        public void Release()
        {
            List<ImportPageViewModel> liveViewModels;
            long version;
            lock (_gate)
            {
                if (!_isActive)
                    return;

                _isActive = false;
                version = ++_version;
                liveViewModels = CollectLiveViewModels();
            }

            PublishState(liveViewModels, value: false, version);
        }

        private void RollbackFailedAcquire(long failedVersion, Exception publishError)
        {
            List<ImportPageViewModel> liveViewModels;
            long rollbackVersion;
            lock (_gate)
            {
                if (!_isActive || _version != failedVersion)
                    return;

                _isActive = false;
                rollbackVersion = ++_version;
                liveViewModels = CollectLiveViewModels();
            }

            try
            {
                PublishState(liveViewModels, value: false, rollbackVersion);
            }
            catch (Exception rollbackError)
            {
                publishError.Data["SharedImportRollbackError"] = rollbackError;
            }
        }

        private List<ImportPageViewModel> CollectLiveViewModels()
        {
            var liveViewModels = new List<ImportPageViewModel>(_viewModels.Count);
            for (var index = _viewModels.Count - 1; index >= 0; index--)
            {
                if (_viewModels[index].TryGetTarget(out var viewModel))
                    liveViewModels.Add(viewModel);
                else
                    _viewModels.RemoveAt(index);
            }

            return liveViewModels;
        }

        private void PublishState(
            IEnumerable<ImportPageViewModel> viewModels,
            bool value,
            long version)
        {
            Exception? firstError = null;
            foreach (var viewModel in viewModels)
            {
                try
                {
                    viewModel.SetSharedImportInProgress(value, version);
                }
                catch (Exception error)
                {
                    firstError ??= error;
                }
            }

            try
            {
                // Die Shell liest den echten Gate-Zustand erneut. Dadurch kann eine
                // spaete Freigabemeldung keinen neu gestarteten Import freischalten.
                OperationAvailabilityChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception error)
            {
                firstError ??= error;
            }

            if (firstError is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo
                    .Capture(firstError)
                    .Throw();
            }
        }
    }
}
