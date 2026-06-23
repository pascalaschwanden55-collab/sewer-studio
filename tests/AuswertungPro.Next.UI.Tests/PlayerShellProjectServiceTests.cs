using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerShellProjectServiceTests
{
    [Fact]
    public void GetCurrentProject_returns_project_from_shell_context()
    {
        var shell = new FakeShellProjectContext();
        var service = new PlayerShellProjectService(() => shell);

        var project = service.GetCurrentProject();

        Assert.Same(shell.Project, project);
    }

    [Fact]
    public void MarkProjectDirty_delegates_to_shell_context()
    {
        var shell = new FakeShellProjectContext();
        var record = new HaltungRecord();
        var service = new PlayerShellProjectService(() => shell);

        var handled = service.MarkProjectDirty(record);

        Assert.True(handled);
        Assert.Same(record, shell.MarkedRecord);
    }

    [Fact]
    public void MarkProjectDirty_returns_false_without_shell_context()
    {
        var service = new PlayerShellProjectService(() => new object());

        var handled = service.MarkProjectDirty(new HaltungRecord());

        Assert.False(handled);
    }

    [Fact]
    public void TrySaveProjectIfReady_saves_only_ready_project()
    {
        var shell = new FakeShellProjectContext { IsProjectReady = true };
        var service = new PlayerShellProjectService(() => shell);

        var saved = service.TrySaveProjectIfReady();

        Assert.True(saved);
        Assert.True(shell.SaveCalled);
    }

    [Fact]
    public void TrySaveProjectIfReady_returns_false_when_project_is_not_ready()
    {
        var shell = new FakeShellProjectContext { IsProjectReady = false };
        var service = new PlayerShellProjectService(() => shell);

        var saved = service.TrySaveProjectIfReady();

        Assert.False(saved);
        Assert.False(shell.SaveCalled);
    }

    private sealed class FakeShellProjectContext : IPlayerShellProjectContext
    {
        public Project Project { get; } = new();
        public bool IsProjectReady { get; init; }
        public bool SaveCalled { get; private set; }
        public HaltungRecord? MarkedRecord { get; private set; }

        public void MarkProjectDirty(HaltungRecord? record = null)
            => MarkedRecord = record;

        public bool TrySaveProject()
        {
            SaveCalled = true;
            return true;
        }
    }
}
