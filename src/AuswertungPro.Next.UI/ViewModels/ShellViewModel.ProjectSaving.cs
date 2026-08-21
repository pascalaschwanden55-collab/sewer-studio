using System.IO;

namespace AuswertungPro.Next.UI.ViewModels;

public sealed partial class ShellViewModel
{
    private readonly object _shellOperationGuardGate = new();
    private readonly HashSet<IShellOperationGuard> _shellOperationGuards = [];
    private IShellOperationGuard? _activeProjectOperationGuard;

    internal void RegisterShellOperationGuard(IShellOperationGuard guard)
    {
        ArgumentNullException.ThrowIfNull(guard);

        lock (_shellOperationGuardGate)
        {
            if (_disposed || !_shellOperationGuards.Add(guard))
                return;

            guard.OperationAvailabilityChanged += OnShellOperationAvailabilityChanged;
        }

        NotifyShellOperationCommands();
    }

    internal Func<bool> CreateActiveImportProjectSaveDelegate(IShellOperationGuard importGuard)
        => CreateActiveProjectOperationSaveDelegate(importGuard);

    internal bool TryAcquireProjectOperation(IShellOperationGuard guard)
    {
        ArgumentNullException.ThrowIfNull(guard);

        lock (_shellOperationGuardGate)
        {
            if (_disposed
                || !_shellOperationGuards.Contains(guard)
                || _activeProjectOperationGuard is not null)
            {
                return false;
            }

            _activeProjectOperationGuard = guard;
        }

        try
        {
            NotifyShellOperationCommands();
            return true;
        }
        catch (Exception startError)
        {
            lock (_shellOperationGuardGate)
            {
                if (ReferenceEquals(_activeProjectOperationGuard, guard))
                    _activeProjectOperationGuard = null;
            }

            try
            {
                NotifyShellOperationCommands();
            }
            catch (Exception rollbackError)
            {
                startError.Data["ProjectOperationAcquireRollbackError"] = rollbackError;
            }

            throw;
        }
    }

    internal void ReleaseProjectOperation(IShellOperationGuard guard)
    {
        ArgumentNullException.ThrowIfNull(guard);

        bool released;
        lock (_shellOperationGuardGate)
        {
            released = ReferenceEquals(_activeProjectOperationGuard, guard);
            if (released)
                _activeProjectOperationGuard = null;
        }

        if (released && !_disposed)
            NotifyShellOperationCommands();
    }

    internal Func<bool> CreateActiveProjectOperationSaveDelegate(IShellOperationGuard operationGuard)
    {
        ArgumentNullException.ThrowIfNull(operationGuard);
        lock (_shellOperationGuardGate)
        {
            if (!_shellOperationGuards.Contains(operationGuard))
            {
                throw new InvalidOperationException(
                    "Der interne Speicherweg braucht einen registrierten Shell-Schutz.");
            }
        }

        return () => TrySaveProjectForActiveOperation(operationGuard);
    }

    private bool TrySaveProjectForActiveOperation(IShellOperationGuard operationGuard)
    {
        bool canSave;
        lock (_shellOperationGuardGate)
        {
            canSave = _shellOperationGuards.Contains(operationGuard)
                      && ReferenceEquals(_activeProjectOperationGuard, operationGuard)
                      && operationGuard.AllowsInternalProjectSave;
        }

        if (!canSave)
        {
            SetStatus("Der interne Speicherweg ist nur waehrend des aktiven Projektvorgangs erlaubt.");
            return false;
        }

        return TrySaveProjectCore();
    }

    internal void UnregisterShellOperationGuard(IShellOperationGuard guard)
    {
        ArgumentNullException.ThrowIfNull(guard);

        bool removed;
        lock (_shellOperationGuardGate)
        {
            removed = _shellOperationGuards.Remove(guard);
        }

        if (!removed)
            return;

        guard.OperationAvailabilityChanged -= OnShellOperationAvailabilityChanged;
        if (!_disposed)
            NotifyShellOperationCommands();
    }

    private void UnregisterShellOperationGuards()
    {
        IShellOperationGuard[] guards;
        lock (_shellOperationGuardGate)
        {
            guards = [.. _shellOperationGuards];
            _shellOperationGuards.Clear();
            _activeProjectOperationGuard = null;
        }

        foreach (var guard in guards)
            guard.OperationAvailabilityChanged -= OnShellOperationAvailabilityChanged;
    }

    private void OnShellOperationAvailabilityChanged(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        if (!_disposed)
            NotifyShellOperationCommands();
    }

    private (IShellOperationGuard? ActiveGuard, IShellOperationGuard[] RegisteredGuards)
        ShellOperationGuardsSnapshot()
    {
        lock (_shellOperationGuardGate)
        {
            return (_activeProjectOperationGuard, [.. _shellOperationGuards]);
        }
    }

    private bool CanSaveProjectFromShell()
    {
        var snapshot = ShellOperationGuardsSnapshot();
        return snapshot.ActiveGuard is null
               && snapshot.RegisteredGuards.All(static guard => guard.CanSaveProjectFromShell);
    }

    private bool ConfirmProjectSaveFromShell()
    {
        var snapshot = ShellOperationGuardsSnapshot();
        var blockingGuard = snapshot.ActiveGuard
                            ?? snapshot.RegisteredGuards
            .FirstOrDefault(static guard => !guard.CanSaveProjectFromShell);
        if (blockingGuard is null)
            return true;

        SetStatus(blockingGuard.ProjectSaveBlockedMessage);
        return false;
    }

    private bool CanLeaveShellContextFromOperationGuards()
    {
        var snapshot = ShellOperationGuardsSnapshot();
        return snapshot.ActiveGuard is null
               && snapshot.RegisteredGuards.All(static guard => guard.CanLeaveShellContext);
    }

    private bool ConfirmOperationGuardsAllowLeave()
    {
        var snapshot = ShellOperationGuardsSnapshot();
        var blockingGuard = snapshot.ActiveGuard
                            ?? snapshot.RegisteredGuards
            .FirstOrDefault(static guard => !guard.CanLeaveShellContext);
        if (blockingGuard is null)
            return true;

        SetStatus(blockingGuard.LeaveBlockedMessage);
        return false;
    }

    /// <summary>
    /// Gemeinsamer Ausgang fuer Navigation, Projektwechsel und Fensterschliessen.
    /// Erst globale Projektvorgaenge, danach den lokalen Seitenschutz pruefen.
    /// </summary>
    public bool ConfirmLeaveCurrentContext()
        => ConfirmOperationGuardsAllowLeave()
           && ShellLeaveGuard.CanLeave(CurrentPage);

    private void NotifyShellOperationCommands()
    {
        SaveCommand?.NotifyCanExecuteChanged();
        SaveAsProjectCommand?.NotifyCanExecuteChanged();
        NewProjectCommand?.NotifyCanExecuteChanged();
        SwitchProjectCommand?.NotifyCanExecuteChanged();
        OpenProjectCommand?.NotifyCanExecuteChanged();
    }

    private void SaveProject()
        => TrySaveProject();

    public bool TrySaveProject()
    {
        // Oeffentliche Aufrufer sind UI-Aktionen, einschliesslich des modeless Players.
        // Nur der separat ausgegebene Import-Delegate darf waehrend des Imports speichern.
        if (!ConfirmProjectSaveFromShell())
            return false;

        return TrySaveProjectCore();
    }

    private bool TrySaveProjectCore()
    {
        // Save nutzt den letzten Pfad NUR, wenn das aktuelle Projekt wirklich von dort
        // stammt (HasPersistedProject). Sonst zeigt LastProjectPath noch auf das zuvor
        // geoeffnete Projekt und "Speichern" wuerde dessen Datei still ueberschreiben.
        var path = NormalizeProjectPath(_sp.Settings.LastProjectPath);
        bool isNewPath = false;
        if (string.IsNullOrWhiteSpace(path) || !HasPersistedProject)
        {
            var defaultName = MakeSafeFileName(Project.Name);
            path = _sp.Dialogs.SaveFile("Projekt speichern", "Projekt (*.json)|*.json", ".json", defaultName);
            if (path is null)
            {
                SetStatus("Speichern abgebrochen");
                return false;
            }
            isNewPath = true;
        }

        EnsureProjectDirectory(path);
        if (_sp.Settings.EnableRestorePoints)
            TryCreateProjectRestorePoint(path);

        var res = _sp.Projects.Save(Project, path);
        if (!res.Ok)
        {
            ShowProjectSaveError(path, res.ErrorMessage);
            SetStatus($"Fehler: {res.ErrorMessage}");
            return false;
        }

        // Merkliste/LastProjectPath erst NACH erfolgreichem Schreiben setzen (Audit P0-5b):
        // bei einem neuen Pfad wuerde ein Schreibfehler sonst LastProjectPath auf eine nie
        // erzeugte Datei zeigen lassen.
        if (isNewPath)
        {
            _sp.Settings.AddRecentProject(NormalizeProjectPath(path)); // setzt auch LastProjectPath
            _sp.Settings.Save();
        }
        IsProjectReady = true;
        HasPersistedProject = true;
        RefreshTitleAndDirty(); // Save setzt Project.Dirty=false -> Marker entfernen
        SetStatus("Gespeichert");
        _sp.Toasts.Success("Projekt gespeichert");
        return true;
    }

    public bool TrySaveProjectAs()
    {
        // Dieser oeffentliche Weg wird auch ausserhalb des Commands aufgerufen.
        // Deshalb reicht CanExecute allein als Schutz nicht aus.
        if (!ConfirmProjectSaveFromShell())
            return false;

        var defaultName = MakeSafeFileName(Project.Name);
        var path = _sp.Dialogs.SaveFile("Projekt speichern unter", "Projekt (*.json)|*.json", ".json", defaultName);
        if (path is null)
        {
            SetStatus("Speichern abgebrochen");
            return false;
        }

        path = NormalizeProjectPath(path);

        // Transaktionale Reihenfolge (Audit P0-5b): ZUERST tatsaechlich speichern. Merkliste,
        // LastProjectPath und "bereit"-Status erst NACH erfolgreichem Schreiben setzen — sonst
        // zeigt LastProjectPath bei einem Schreibfehler auf eine Datei, die es nie gab.
        EnsureProjectDirectory(path);
        if (_sp.Settings.EnableRestorePoints)
            TryCreateProjectRestorePoint(path);

        var res = _sp.Projects.Save(Project, path);
        if (!res.Ok)
        {
            ShowProjectSaveError(path, res.ErrorMessage);
            SetStatus($"Fehler: {res.ErrorMessage}");
            return false;
        }

        _sp.Settings.AddRecentProject(path); // Merkliste pflegen (setzt auch LastProjectPath)
        _sp.Settings.Save();
        MarkProjectReady();
        HasPersistedProject = true;
        RefreshTitleAndDirty(); // Save setzt Project.Dirty=false -> Marker entfernen
        SetStatus($"Gespeichert: {Path.GetFileName(path)}");
        _sp.Toasts.Success($"Gespeichert: {Path.GetFileName(path)}");
        return true;
    }

    private void ShowProjectSaveError(string path, string? error)
    {
        _sp.Dialogs.Error(
            "Das Projekt konnte nicht gespeichert werden. Die vorhandene Projektdatei wurde nicht geloescht.\n\n" +
            $"Ziel: {path}\n" +
            $"Fehler: {error}\n\n" +
            "Bitte pruefe freien Speicherplatz, Schreibschutz und Zugriffsrechte. " +
            "Versuche danach 'Speichern unter' in einem anderen Ordner.",
            "Projekt nicht gespeichert");
    }

    private void SaveProjectAs()
        => TrySaveProjectAs();
}
