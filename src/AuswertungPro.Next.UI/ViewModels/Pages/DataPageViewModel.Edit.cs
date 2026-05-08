using System;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

// Bearbeitungs-Befehle: Add/Remove/Move + UpdateNr (laufende Nummer) +
// Save/AutoSave-Zyklus mit DispatcherTimer.
public sealed partial class DataPageViewModel
{
    /// <summary>
    /// Aktualisiert die laufende Nummer (NR) aller Records entsprechend der aktuellen Reihenfolge.
    /// </summary>
    private void UpdateNr()
    {
        for (int i = 0; i < Records.Count; i++)
        {
            Records[i].SetFieldValue("NR", (i + 1).ToString(), FieldSource.Manual, true);
        }
    }

    private bool CanMoveUp()
    {
        if (Selected is null) return false;
        var idx = Records.IndexOf(Selected);
        return idx > 0;
    }

    private bool CanMoveDown()
    {
        if (Selected is null) return false;
        var idx = Records.IndexOf(Selected);
        return idx >= 0 && idx < Records.Count - 1;
    }

    private void Add()
    {
        var record = _shell.Project.CreateNewRecord();
        _shell.Project.AddRecord(record);
        Selected = record;
        ScheduleAutoSave();
    }

    private void Remove()
    {
        if (Selected is null) return;

        var idx = Records.IndexOf(Selected);
        var removedId = Selected.Id;
        var removed = _shell.Project.RemoveRecord(removedId);
        if (!removed)
        {
            return;
        }

        if (Records.Count == 0)
        {
            Selected = null;
            ScheduleAutoSave();
            return;
        }

        if (idx >= Records.Count) idx = Records.Count - 1;
        Selected = Records[idx];
        ScheduleAutoSave();
    }

    private void MoveUp()
    {
        if (Selected is null) return;
        var idx = Records.IndexOf(Selected);
        if (idx <= 0) return;
        Records.Move(idx, idx - 1);
        UpdateNr();
        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        RecordsOrderChanged?.Invoke();
        ScheduleAutoSave();
    }

    private void MoveDown()
    {
        if (Selected is null) return;
        var idx = Records.IndexOf(Selected);
        if (idx < 0 || idx >= Records.Count - 1) return;
        Records.Move(idx, idx + 1);
        UpdateNr();
        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        RecordsOrderChanged?.Invoke();
        ScheduleAutoSave();
    }

    /// <summary>
    /// Verschiebt die aktuell selektierte Haltung an die angegebene 1-basierte Position.
    /// Alle Zeilen ab dieser Position rutschen um eins nach unten.
    /// </summary>
    public bool MoveToPosition(int targetPosition)
    {
        if (Selected is null) return false;
        var idx = Records.IndexOf(Selected);
        if (idx < 0) return false;

        // 1-basiert -> 0-basiert
        int targetIdx = targetPosition - 1;
        if (targetIdx < 0) targetIdx = 0;
        if (targetIdx >= Records.Count) targetIdx = Records.Count - 1;
        if (targetIdx == idx) return false;

        Records.Move(idx, targetIdx);
        UpdateNr();
        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        RecordsOrderChanged?.Invoke();
        ScheduleAutoSave();
        return true;
    }

    private void Save()
    {
        var learnedAny = false;
        foreach (var record in Records)
            learnedAny |= _measureRecommendationService.Learn(record);
        if (learnedAny)
            _measureRecommendationService.TrainModel(MinimumSamplesForModelTraining);
        UpdateLearningInfo();

        SaveDropdownOptions();
        var ok = _shell.TrySaveProject();
        ShowSaveStatus(_shell.Subtitle);
        if (!ok)
            IsSaveStatusVisible = true;
    }

    /// <summary>
    /// Schedules auto-save according to settings.
    /// </summary>
    public void ScheduleAutoSave()
    {
        _shell.Project.Dirty = true;
        var mode = App.Resolve<AppSettings>().DataAutoSaveMode.Normalize();
        switch (mode)
        {
            case AutoSaveMode.OnEachChange:
                _autoSaveTimer.Stop();
                AutoSave();
                break;
            case AutoSaveMode.Every5Minutes:
            case AutoSaveMode.Every10Minutes:
                ScheduleIntervalAutoSave(mode);
                break;
            case AutoSaveMode.Disabled:
                _autoSaveTimer.Stop();
                break;
            default:
                _autoSaveTimer.Stop();
                AutoSave();
                break;
        }
    }

    private void ScheduleIntervalAutoSave(AutoSaveMode mode)
    {
        var interval = mode.GetInterval();
        if (interval is null)
        {
            _autoSaveTimer.Stop();
            return;
        }

        if (_autoSaveTimer.Interval != interval.Value)
            _autoSaveTimer.Interval = interval.Value;

        if (!_autoSaveTimer.IsEnabled)
            _autoSaveTimer.Start();
    }

    private void AutoSaveOnTimerTick()
    {
        var mode = App.Resolve<AppSettings>().DataAutoSaveMode.Normalize();
        if (mode is not (AutoSaveMode.Every5Minutes or AutoSaveMode.Every10Minutes))
        {
            _autoSaveTimer.Stop();
            return;
        }

        AutoSave();

        // No pending changes left -> no need to keep ticking.
        if (!_shell.Project.Dirty)
            _autoSaveTimer.Stop();
    }

    private void AutoSave()
    {
        if (!_shell.IsProjectReady || !_shell.Project.Dirty)
            return;

        SaveDropdownOptions();
        var ok = _shell.TrySaveProject();
        if (ok)
            ShowSaveStatus("Automatisch gespeichert");
    }
}
