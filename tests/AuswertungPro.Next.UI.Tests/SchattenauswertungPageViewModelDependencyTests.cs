using AuswertungPro.Next.Application.Schatten;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchattenauswertungPageViewModelDependencyTests
{
    [Fact]
    public void Konstruktor_laesst_store_und_projektpfad_gezielt_injizieren()
    {
        var repository = new FakeStore();

        var viewModel = new SchattenauswertungPageViewModel(
            getProject: () => null,
            store: repository,
            createService: () => new FakeService(new SchattenAuswertungStore()),
            getProjectPath: () => @"C:\Projekt\projekt.json");

        Assert.Equal(@"C:\Projekt\projekt.json", repository.LastLoadPath);
        Assert.Equal("Noch kein Lauf für dieses Projekt.", viewModel.StatusText);
    }

    [Fact]
    public async Task StartenCommand_nutzt_injizierten_service_und_speichert_ergebnis()
    {
        var project = ProjectWithRecord("100-200");
        var repository = new FakeStore();
        var calculated = new SchattenAuswertungStore
        {
            LetzterLaufUtc = new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc)
        };
        var service = new FakeService(calculated);
        var viewModel = new SchattenauswertungPageViewModel(
            getProject: () => project,
            store: repository,
            createService: () => service,
            getProjectPath: () => @"C:\Projekt\projekt.json");

        await viewModel.StartenCommand.ExecuteAsync(null);

        Assert.Equal(1, service.CallCount);
        Assert.Same(project, service.LastProject);
        Assert.Equal(1, repository.SaveCount);
        Assert.Same(calculated, repository.LastSavedStore);
        Assert.Equal("Lauf abgeschlossen.", viewModel.StatusText);
    }

    [Fact]
    public async Task StartenCommand_zeigt_keine_rohen_technischen_fehlerdetails()
    {
        var viewModel = new SchattenauswertungPageViewModel(
            getProject: () => ProjectWithRecord("100-200"),
            store: new FakeStore(),
            createService: () => throw new InvalidOperationException("internes Geheimdetail"),
            getProjectPath: () => @"C:\Projekt\projekt.json");

        await viewModel.StartenCommand.ExecuteAsync(null);

        Assert.DoesNotContain("Geheimdetail", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Programmlog", viewModel.StatusText, StringComparison.Ordinal);
    }

    private static Project ProjectWithRecord(string haltung)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", haltung, FieldSource.Manual, userEdited: false);
        var project = new Project();
        project.Data.Add(record);
        return project;
    }

    private sealed class FakeStore : ISchattenAuswertungStore
    {
        public string? LastLoadPath { get; private set; }
        public int SaveCount { get; private set; }
        public SchattenAuswertungStore? LastSavedStore { get; private set; }

        public SchattenAuswertungStore Load(string? projectPath, out string? loadError)
        {
            LastLoadPath = projectPath;
            loadError = null;
            return new SchattenAuswertungStore();
        }

        public bool Save(string? projectPath, SchattenAuswertungStore store, out string error)
        {
            SaveCount++;
            LastSavedStore = store;
            error = string.Empty;
            return true;
        }
    }

    private sealed class FakeService(SchattenAuswertungStore result) : ISchattenAuswertungService
    {
        public int CallCount { get; private set; }
        public Project? LastProject { get; private set; }

        public Task<SchattenAuswertungStore> BerechneAsync(
            Project projekt,
            bool mitKi,
            IProgress<SchattenFortschritt>? fortschritt,
            Action<SchattenAuswertungStore>? zwischenspeichern,
            CancellationToken ct)
        {
            CallCount++;
            LastProject = projekt;
            return Task.FromResult(result);
        }
    }
}
