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

    private sealed class RecordingCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool WasExecuted { get; private set; }
        public object? Parameter { get; private set; }

        public bool CanExecute(object? parameter)
        {
            _ = parameter;
            return true;
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
