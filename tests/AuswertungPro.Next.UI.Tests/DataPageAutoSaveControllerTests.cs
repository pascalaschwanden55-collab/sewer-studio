using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageAutoSaveControllerTests
{
    [Fact]
    public void Schedule_on_each_change_restarts_short_timer_without_saving_immediately()
    {
        var calls = new List<string>();

        DataPageAutoSaveController.Schedule(
            AutoSaveMode.OnEachChange,
            markDirty: () => calls.Add("dirty"),
            stopTimer: () => calls.Add("stop"),
            setInterval: interval => calls.Add($"interval:{interval.TotalMilliseconds}"),
            isTimerEnabled: () => true,
            startTimer: () => calls.Add("start"));

        Assert.Equal(new[] { "dirty", "stop", "interval:750", "start" }, calls);
    }

    [Fact]
    public void Schedule_interval_mode_marks_dirty_sets_interval_and_starts_stopped_timer()
    {
        var calls = new List<string>();

        DataPageAutoSaveController.Schedule(
            AutoSaveMode.Every10Minutes,
            markDirty: () => calls.Add("dirty"),
            stopTimer: () => calls.Add("stop"),
            setInterval: interval => calls.Add($"interval:{interval.TotalMinutes}"),
            isTimerEnabled: () => false,
            startTimer: () => calls.Add("start"));

        Assert.Equal(new[] { "dirty", "interval:10", "start" }, calls);
    }

    [Fact]
    public void Schedule_interval_mode_does_not_restart_running_timer()
    {
        var calls = new List<string>();

        DataPageAutoSaveController.Schedule(
            AutoSaveMode.Every5Minutes,
            markDirty: () => calls.Add("dirty"),
            stopTimer: () => calls.Add("stop"),
            setInterval: interval => calls.Add($"interval:{interval.TotalMinutes}"),
            isTimerEnabled: () => true,
            startTimer: () => calls.Add("start"));

        Assert.Equal(new[] { "dirty", "interval:5" }, calls);
    }

    [Fact]
    public void Schedule_disabled_marks_dirty_and_stops_timer_without_saving()
    {
        var calls = new List<string>();

        DataPageAutoSaveController.Schedule(
            AutoSaveMode.Disabled,
            markDirty: () => calls.Add("dirty"),
            stopTimer: () => calls.Add("stop"),
            setInterval: interval => calls.Add($"interval:{interval.TotalMinutes}"),
            isTimerEnabled: () => true,
            startTimer: () => calls.Add("start"));

        Assert.Equal(new[] { "dirty", "stop" }, calls);
    }

    [Fact]
    public void Timer_tick_in_interval_mode_saves_and_stops_when_project_is_clean()
    {
        var calls = new List<string>();

        DataPageAutoSaveController.HandleTimerTick(
            AutoSaveMode.Every5Minutes,
            save: () => calls.Add("save"),
            isProjectDirty: () => false,
            stopTimer: () => calls.Add("stop"));

        Assert.Equal(new[] { "save", "stop" }, calls);
    }

    [Fact]
    public void Timer_tick_in_on_each_change_mode_stops_and_saves_once()
    {
        var calls = new List<string>();

        DataPageAutoSaveController.HandleTimerTick(
            AutoSaveMode.OnEachChange,
            save: () => calls.Add("save"),
            isProjectDirty: () => true,
            stopTimer: () => calls.Add("stop"));

        Assert.Equal(new[] { "stop", "save" }, calls);
    }
}
