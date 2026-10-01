using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class SchaechtePageViewModel
{
    private DataPageTimerController? _autoSaveTimers;
    private Project? _autoSaveProject;

    public void ScheduleAutoSave()
    {
        _autoSaveProject = _shell.Project;
        _autoSaveTimers ??= new DataPageTimerController(
            text => LastResult = text, _ => { }, SavePendingShaftChanges);
        _autoSaveTimers.ScheduleAutoSave(_settings.DataAutoSaveMode, () =>
        {
            _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
            _shell.Project.Dirty = true;
        });
    }

    private void SavePendingShaftChanges()
    {
        // Ein verspäteter Timer darf niemals ein inzwischen geöffnetes anderes Projekt speichern.
        if (!ReferenceEquals(_autoSaveProject, _shell.Project))
        {
            _autoSaveTimers?.Stop();
            return;
        }

        _autoSaveTimers?.HandleAutoSaveTimerTick(_settings.DataAutoSaveMode, () =>
        {
            if (_shell.IsProjectReady && _shell.Project.Dirty && _shell.TrySaveProject())
                LastResult = "Schächte automatisch gespeichert.";
        }, () => _shell.Project.Dirty);
    }
}
