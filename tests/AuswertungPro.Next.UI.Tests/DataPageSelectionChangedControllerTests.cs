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
