using System.Windows.Input;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageRecordCommandRouterTests
{
    [Fact]
    public void DataPage_simple_record_menu_handlers_use_record_command_router()
    {
        var source = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "DataPage.xaml.cs"));
        var simpleMenuHandlers = ExtractBetween(
            source,
            "private void PlayMenu_Click",
            "private void SuggestAllMeasuresMenu_Click");

        Assert.Contains("DataPageRecordCommandRouter.TryExecute", source);
        Assert.DoesNotContain("vm.PlayVideoCommand.Execute(record);", simpleMenuHandlers);
        Assert.DoesNotContain("vm.RelinkVideoCommand.Execute(record);", simpleMenuHandlers);
        Assert.DoesNotContain("vm.OpenCostsCommand.Execute(record);", simpleMenuHandlers);
        Assert.DoesNotContain("vm.PrintAwuHaltungsprotokollCommand.Execute(record);", simpleMenuHandlers);
        Assert.DoesNotContain("vm.OpenOriginalPdfCommand.Execute(record);", simpleMenuHandlers);
        Assert.DoesNotContain("vm.RestoreCostsCommand.Execute(record);", simpleMenuHandlers);
        Assert.DoesNotContain("vm.SuggestMeasuresCommand.Execute(record);", simpleMenuHandlers);
    }

    [Fact]
    public void DataPage_move_record_menu_handlers_use_record_command_router()
    {
        var source = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "DataPage.xaml.cs"));
        var moveMenuHandlers = ExtractBetween(
            source,
            "private void MoveRecordUpMenu_Click",
            "private void DropdownButton_Click");

        Assert.Contains("DataPageRecordCommandRouter.TrySelectAndExecute", source);
        Assert.DoesNotContain("vm.Selected = record;", moveMenuHandlers);
        Assert.DoesNotContain("vm.MoveUpCommand.CanExecute(null)", moveMenuHandlers);
        Assert.DoesNotContain("vm.MoveDownCommand.CanExecute(null)", moveMenuHandlers);
        Assert.DoesNotContain("vm.MoveUpCommand.Execute(null)", moveMenuHandlers);
        Assert.DoesNotContain("vm.MoveDownCommand.Execute(null)", moveMenuHandlers);
    }


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

    private static string RepoFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "AuswertungPro.sln");
            if (File.Exists(candidate))
                return Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private static string ExtractBetween(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Start marker not found: {startMarker}");
        Assert.True(end > start, $"End marker not found: {endMarker}");
        return source[start..end];
    }
}
