namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class ExportPageViewModel
{
    private Task RunWithProjectOperationAsync(
        Func<Task> operation,
        bool allowsInternalProjectSave = false)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!TryBeginProjectOperation(allowsInternalProjectSave))
            return Task.CompletedTask;

        return RunAcquiredProjectOperationAsync(operation);
    }

    private async Task RunAcquiredProjectOperationAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        finally
        {
            EndProjectOperation();
        }
    }

    private void RunXtfRevisionWithProjectOperation()
    {
        if (!TryBeginProjectOperation(allowsInternalProjectSave: false))
            return;

        try
        {
            ErzeugeXtfRevision();
        }
        finally
        {
            EndProjectOperation();
        }
    }

    private bool TryBeginProjectOperation(bool allowsInternalProjectSave)
    {
        if (_disposed || !_shell.TryAcquireProjectOperation(_shellOperationGuard))
        {
            _shell.SetStatus("Es laeuft bereits ein Import, Export oder anderer Projektvorgang.");
            return false;
        }

        try
        {
            SetShaftDistributionActive(allowsInternalProjectSave);
            IsPageBusy = true;
            return true;
        }
        catch (Exception startError)
        {
            try
            {
                EndProjectOperation();
            }
            catch (Exception rollbackError)
            {
                startError.Data["ExportOperationStartRollbackError"] = rollbackError;
            }

            throw;
        }
    }

    private void EndProjectOperation()
    {
        Exception? firstError = null;
        var additionalErrorIndex = 0;

        TryReleaseStep(() => SetShaftDistributionActive(false));
        TryReleaseStep(() => IsPageBusy = false);
        TryReleaseStep(() => _shell.ReleaseProjectOperation(_shellOperationGuard));

        if (firstError is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(firstError)
                .Throw();
        }

        void TryReleaseStep(Action releaseStep)
        {
            try
            {
                releaseStep();
            }
            catch (Exception error)
            {
                if (firstError is null)
                    firstError = error;
                else
                    firstError.Data[$"ExportOperationReleaseError{++additionalErrorIndex}"] = error;
            }
        }
    }

    private sealed class ExportPageShellOperationGuard : IShellOperationGuard
    {
        private readonly object _gate = new();
        private bool _isBusy;
        private bool _allowsInternalProjectSave;

        public bool CanSaveProjectFromShell
        {
            get
            {
                lock (_gate)
                    return !_isBusy;
            }
        }

        public string ProjectSaveBlockedMessage
            => "Manuelles Speichern ist waehrend eines Exports oder einer Verteilung gesperrt. " +
               "Bitte den laufenden Vorgang zuerst abschliessen.";

        public bool AllowsInternalProjectSave
        {
            get
            {
                lock (_gate)
                    return _isBusy && _allowsInternalProjectSave;
            }
        }

        public bool CanLeaveShellContext
        {
            get
            {
                lock (_gate)
                    return !_isBusy;
            }
        }

        public string LeaveBlockedMessage
            => "Navigation, Projektwechsel und Schliessen sind waehrend eines Exports " +
               "oder einer Verteilung gesperrt. Bitte den laufenden Vorgang zuerst abschliessen.";

        public event EventHandler? OperationAvailabilityChanged;

        internal void Update(bool isBusy, bool allowsInternalProjectSave)
        {
            var normalizedInternalSave = isBusy && allowsInternalProjectSave;
            lock (_gate)
            {
                if (_isBusy == isBusy
                    && _allowsInternalProjectSave == normalizedInternalSave)
                {
                    return;
                }

                _isBusy = isBusy;
                _allowsInternalProjectSave = normalizedInternalSave;
            }

            OperationAvailabilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
