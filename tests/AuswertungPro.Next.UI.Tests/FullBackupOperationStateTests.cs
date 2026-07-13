using AuswertungPro.Next.UI.Settings;

namespace AuswertungPro.Next.UI.Tests;

public sealed class FullBackupOperationStateTests
{
    [Fact]
    public void TryBegin_marks_running_and_blocks_a_second_backup()
    {
        var state = new FullBackupOperationState();

        Assert.True(state.TryBegin(CancellationToken.None, out var runToken));
        Assert.True(state.IsRunning);
        Assert.False(runToken.IsCancellationRequested);
        Assert.Equal("Berechne Groessen...", state.StatusText);
        Assert.False(state.TryBegin(CancellationToken.None, out _));

        state.Finish();
        Assert.False(state.IsRunning);
    }

    [Fact]
    public void UpdateProgress_clamps_percent_and_keeps_shared_values()
    {
        var state = new FullBackupOperationState();

        state.UpdateProgress(140, "projekt.json", "Projekte: 4 von 5 Dateien");
        state.SetLastBackupInfo("Letzte Datensicherung: heute");

        Assert.Equal(100, state.Percent);
        Assert.Equal("projekt.json", state.CurrentFile);
        Assert.Equal("Projekte: 4 von 5 Dateien", state.StatusText);
        Assert.Equal("Letzte Datensicherung: heute", state.LastBackupInfo);
    }

    [Fact]
    public void Cancel_cancels_the_shared_run_token()
    {
        var state = new FullBackupOperationState();
        Assert.True(state.TryBegin(CancellationToken.None, out var runToken));

        state.Cancel();

        Assert.True(runToken.IsCancellationRequested);
        Assert.Equal("Abbruch wird ausgefuehrt...", state.StatusText);
        state.Finish();
    }

    [Fact]
    public void Finish_hides_progress_but_keeps_the_result_message()
    {
        var state = new FullBackupOperationState();
        Assert.True(state.TryBegin(CancellationToken.None, out _));
        state.UpdateProgress(100, "projekt.json", "Fertig");

        state.Finish();

        Assert.False(state.IsRunning);
        Assert.Equal(100, state.Percent);
        Assert.Equal(string.Empty, state.CurrentFile);
        Assert.Equal("Fertig", state.StatusText);
    }
}
