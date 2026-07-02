using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageRecordCommandRouterTests
{
    [Fact]
    public void TryExecute_executes_command_with_resolved_record()
    {
        var record = new HaltungRecord();
        var command = new RecordingCommand();
        var dialogs = new List<(string Message, string Title)>();

        var executed = DataPageRecordCommandRouter.TryExecute(
            record,
            command,
            (message, title) => dialogs.Add((message, title)),
            missingSelectionTitle: "Video");

        Assert.True(executed);
        Assert.Same(record, command.Parameter);
        Assert.Empty(dialogs);
    }

    [Fact]
    public void TryExecute_reports_missing_selection_without_executing_command()
    {
        var command = new RecordingCommand();
        var dialogs = new List<(string Message, string Title)>();

        var executed = DataPageRecordCommandRouter.TryExecute(
            record: null,
            command,
            (message, title) => dialogs.Add((message, title)),
            missingSelectionTitle: "PDF");

        Assert.False(executed);
        Assert.False(command.WasExecuted);
        Assert.Equal(("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken oder zuerst eine Zeile auswaehlen.", "PDF"), Assert.Single(dialogs));
    }

    [Fact]
    public void TrySelectAndExecute_selects_record_then_executes_parameterless_command_when_allowed()
    {
        var record = new HaltungRecord();
        var command = new RecordingCommand();
        HaltungRecord? selected = null;
        var dialogs = new List<(string Message, string Title)>();

        var executed = DataPageRecordCommandRouter.TrySelectAndExecute(
            record,
            value => selected = value,
            command,
            (message, title) => dialogs.Add((message, title)),
            missingSelectionTitle: "Position");

        Assert.True(executed);
        Assert.Same(record, selected);
        Assert.True(command.WasExecuted);
        Assert.Null(command.Parameter);
        Assert.Empty(dialogs);
    }

    [Fact]
    public void TrySelectAndExecute_selects_record_without_executing_when_command_is_not_allowed()
    {
        var record = new HaltungRecord();
        var command = new RecordingCommand(canExecute: false);
        HaltungRecord? selected = null;
        var dialogs = new List<(string Message, string Title)>();

        var executed = DataPageRecordCommandRouter.TrySelectAndExecute(
            record,
            value => selected = value,
            command,
            (message, title) => dialogs.Add((message, title)),
            missingSelectionTitle: "Position");

        Assert.False(executed);
        Assert.Same(record, selected);
        Assert.False(command.WasExecuted);
        Assert.Empty(dialogs);
    }

    [Fact]
    public void TrySelectAndExecute_reports_missing_selection_without_selecting_or_executing()
    {
        var command = new RecordingCommand();
        HaltungRecord? selected = null;
        var dialogs = new List<(string Message, string Title)>();

        var executed = DataPageRecordCommandRouter.TrySelectAndExecute(
            record: null,
            value => selected = value,
            command,
            (message, title) => dialogs.Add((message, title)),
            missingSelectionTitle: "Position");

        Assert.False(executed);
        Assert.Null(selected);
        Assert.False(command.WasExecuted);
        Assert.Equal(("Keine Zeile erkannt. Bitte zuerst eine Haltung auswaehlen.", "Position"), Assert.Single(dialogs));
    }

    private sealed class RecordingCommand : ICommand
    {
        private readonly bool _canExecute;

        public RecordingCommand(bool canExecute = true)
        {
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool WasExecuted { get; private set; }
        public object? Parameter { get; private set; }

        public bool CanExecute(object? parameter)
        {
            _ = parameter;
            return _canExecute;
        }

        public void Execute(object? parameter)
        {
            WasExecuted = true;
            Parameter = parameter;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
