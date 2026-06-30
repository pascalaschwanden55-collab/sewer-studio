using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageSelectionChangedControllerTests
{
    [Fact]
    public void Handle_notifiziert_commands_und_synchronisiert_selected_record()
    {
        var calls = new List<string>();
        var record = new HaltungRecord();
        var first = new TestRelayCommand("first", calls);
        var second = new TestRelayCommand("second", calls);

        DataPageSelectionChangedController.Handle(
            record,
            new IRelayCommand?[] { first, null, second },
            normalizeSelectedFindings: _ => calls.Add("normalize"),
            syncSelectedProtocolFromFindings: _ => calls.Add("sync"),
            refreshSelectedProtocolEntries: () => calls.Add("refresh"));

        Assert.Equal(
            new[] { "command:first", "command:second", "normalize", "sync", "refresh" },
            calls);
    }

    [Fact]
    public void Handle_ohne_selected_record_notifiziert_commands_und_refresh_only()
    {
        var calls = new List<string>();
        var command = new TestRelayCommand("remove", calls);

        DataPageSelectionChangedController.Handle(
            selected: null,
            new IRelayCommand?[] { command },
            normalizeSelectedFindings: _ => calls.Add("normalize"),
            syncSelectedProtocolFromFindings: _ => calls.Add("sync"),
            refreshSelectedProtocolEntries: () => calls.Add("refresh"));

        Assert.Equal(new[] { "command:remove", "refresh" }, calls);
    }

    [Fact]
    public void DataPageViewModel_delegiert_selected_change_an_controller()
    {
        var source = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "DataPageViewModel.cs"));
        var method = ExtractMethodBody(source, "private void DataPageViewModel_PropertyChanged");

        Assert.Contains("DataPageSelectionChangedController.Handle(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("NotifyCanExecuteChanged", method, StringComparison.Ordinal);
        Assert.DoesNotContain("NormalizeSelectedFindings(Selected)", method, StringComparison.Ordinal);
        Assert.DoesNotContain("SyncSelectedProtocolFromFindings(Selected)", method, StringComparison.Ordinal);
    }

    private static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Repo-Datei nicht gefunden.", Path.Combine(parts));
    }

    private static string ExtractMethodBody(string source, string signature)
    {
        var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Signatur nicht gefunden: {signature}");

        var braceIndex = source.IndexOf('{', signatureIndex);
        Assert.True(braceIndex >= 0, $"Methodenrumpf nicht gefunden: {signature}");

        var depth = 0;
        for (var i = braceIndex; i < source.Length; i++)
        {
            if (source[i] == '{')
                depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return source[braceIndex..(i + 1)];
            }
        }

        throw new InvalidOperationException($"Methodenrumpf nicht abgeschlossen: {signature}");
    }

    private sealed class TestRelayCommand : IRelayCommand
    {
        private readonly string _name;
        private readonly List<string> _calls;

        public TestRelayCommand(string name, List<string> calls)
        {
            _name = name;
            _calls = calls;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
        }

        public void NotifyCanExecuteChanged()
        {
            _calls.Add($"command:{_name}");
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
