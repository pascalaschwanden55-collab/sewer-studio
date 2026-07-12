using System.Windows.Threading;

namespace AuswertungPro.Next.UI.DataPage;

public sealed class DataPageTimerController
{
    private readonly Action<string> _setSaveStatus;
    private readonly Action<bool> _setSaveVisible;
    private readonly DispatcherTimer _saveBannerTimer;
    private readonly DispatcherTimer _autoSaveTimer;

    public DataPageTimerController(
        Action<string> setSaveStatus,
        Action<bool> setSaveVisible,
        Action? autoSaveTimerTick = null)
    {
        _setSaveStatus = setSaveStatus ?? throw new ArgumentNullException(nameof(setSaveStatus));
        _setSaveVisible = setSaveVisible ?? throw new ArgumentNullException(nameof(setSaveVisible));

        _saveBannerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _saveBannerTimer.Tick += (_, _) => DataPageSaveStatusController.Hide(
            _saveBannerTimer.Stop,
            _setSaveVisible);

        _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        _autoSaveTimer.Tick += (_, _) => autoSaveTimerTick?.Invoke();
    }

    public void ScheduleAutoSave(
        AutoSaveMode mode,
        Action markDirty)
    {
        DataPageAutoSaveController.Schedule(
            mode,
            markDirty,
            stopTimer: _autoSaveTimer.Stop,
            setInterval: interval =>
            {
                if (_autoSaveTimer.Interval != interval)
                    _autoSaveTimer.Interval = interval;
            },
            isTimerEnabled: () => _autoSaveTimer.IsEnabled,
            startTimer: _autoSaveTimer.Start);
    }

    public void HandleAutoSaveTimerTick(
        AutoSaveMode mode,
        Action save,
        Func<bool> isProjectDirty)
    {
        DataPageAutoSaveController.HandleTimerTick(
            mode,
            save,
            isProjectDirty,
            stopTimer: _autoSaveTimer.Stop);
    }

    public void ShowSaveStatus(string? text)
    {
        DataPageSaveStatusController.Show(
            text,
            _setSaveStatus,
            _setSaveVisible,
            _saveBannerTimer.Stop,
            _saveBannerTimer.Start);
    }

    public void Stop()
    {
        _saveBannerTimer.Stop();
        _autoSaveTimer.Stop();
    }
}
