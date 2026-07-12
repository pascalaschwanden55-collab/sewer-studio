using System.Collections.ObjectModel;
using System.ComponentModel;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageProjectBindingControllerTests
{
    [Fact]
    public void Start_uebernimmt_tolerante_kartenauswahl_ohne_echo()
    {
        using var harness = new BindingHarness();
        var record = CreateRecord("21731-21730");
        harness.Records.Add(record);
        harness.MapSelection = "21730-21731.1";

        harness.Controller.Start();

        Assert.Same(record, harness.Selected);
        Assert.Empty(harness.MapWrites);
        Assert.Equal(1, harness.NormalizeCount);
        Assert.Equal(1, harness.SyncCount);
        Assert.Equal(1, harness.RefreshCount);
    }

    [Fact]
    public void HandleSelectedChanged_meldet_benutzerauswahl_an_karte()
    {
        using var harness = new BindingHarness();
        var record = CreateRecord("06-123");
        harness.Controller.Start();

        harness.Controller.HandleSelectedChanged(record);

        Assert.Equal(new[] { "06-123" }, harness.MapWrites);
        Assert.Equal(1, harness.NormalizeCount);
        Assert.Equal(1, harness.SyncCount);
        Assert.Equal(1, harness.RefreshCount);
    }

    [Fact]
    public void Projektwechsel_nummeriert_neue_liste_und_loest_alte_liste()
    {
        using var harness = new BindingHarness();
        var oldRecord = CreateRecord("alt");
        harness.Records.Add(oldRecord);
        harness.Controller.Start();
        Assert.Equal("1", oldRecord.GetFieldValue("NR"));

        var oldRecords = harness.Records;
        var first = CreateRecord("neu-1");
        var second = CreateRecord("neu-2");
        harness.Records = new ObservableCollection<HaltungRecord> { first, second };
        harness.RaiseProjectStateChanged("Project");

        Assert.Equal(1, harness.ProjectChangedCount);
        Assert.Equal("1", first.GetFieldValue("NR"));
        Assert.Equal("2", second.GetFieldValue("NR"));

        var oldAddedLater = CreateRecord("alt-2");
        oldRecords.Add(oldAddedLater);
        Assert.Equal(string.Empty, oldAddedLater.GetFieldValue("NR"));

        var third = CreateRecord("neu-3");
        harness.Records.Add(third);
        Assert.Equal("3", third.GetFieldValue("NR"));
    }

    [Fact]
    public void FindRecordByName_bevorzugt_exakten_treffer_vor_tolerantem()
    {
        var tolerant = CreateRecord("21730-21731");
        var exact = CreateRecord("21731-21730");

        var result = DataPageProjectBindingController.FindRecordByName(
            new[] { tolerant, exact },
            "21731-21730");

        Assert.Same(exact, result);
    }

    [Fact]
    public void Dispose_entfernt_alle_ereignisse()
    {
        var harness = new BindingHarness();
        harness.Controller.Start();
        harness.Controller.Dispose();

        harness.RaiseProjectStateChanged("IsProjectReady");
        harness.MapSelection = "06-123";
        harness.RaiseMapSelectionChanged();

        Assert.Equal(0, harness.ReadinessChangedCount);
        Assert.Null(harness.Selected);
    }

    private static HaltungRecord CreateRecord(string name)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", name, FieldSource.Manual, userEdited: true);
        return record;
    }

    private sealed class BindingHarness : IDisposable
    {
        private event PropertyChangedEventHandler? ProjectStateChanged;
        private event Action? MapSelectionChanged;

        public BindingHarness()
        {
            DataPageProjectBindingController? controller = null;
            controller = new DataPageProjectBindingController(
                handler => ProjectStateChanged += handler,
                handler => ProjectStateChanged -= handler,
                handler => MapSelectionChanged += handler,
                handler => MapSelectionChanged -= handler,
                () => ProjectId,
                () => Records,
                () => Selected,
                value =>
                {
                    Selected = value;
                    controller!.HandleSelectedChanged(value);
                },
                _ => MapSelection,
                value => MapWrites.Add(value),
                action => action(),
                () => ReadinessChangedCount++,
                () => ProjectChangedCount++,
                Array.Empty<IRelayCommand?>(),
                _ => NormalizeCount++,
                _ => SyncCount++,
                () => RefreshCount++);
            Controller = controller;
        }

        public DataPageProjectBindingController Controller { get; }
        public Guid ProjectId { get; } = Guid.NewGuid();
        public ObservableCollection<HaltungRecord> Records { get; set; } = new();
        public HaltungRecord? Selected { get; private set; }
        public string? MapSelection { get; set; }
        public List<string?> MapWrites { get; } = new();
        public int ReadinessChangedCount { get; private set; }
        public int ProjectChangedCount { get; private set; }
        public int NormalizeCount { get; private set; }
        public int SyncCount { get; private set; }
        public int RefreshCount { get; private set; }

        public void RaiseProjectStateChanged(string propertyName)
            => ProjectStateChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void RaiseMapSelectionChanged() => MapSelectionChanged?.Invoke();

        public void Dispose() => Controller.Dispose();
    }
}
