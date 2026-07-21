using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Sanierung;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;
using System.IO;
using System.Net.Http;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageSanierungViewModelFactoryTests
{
    [Fact]
    public void Create_baut_Kostenansicht_und_synchronisiert_das_Projekt_nach_dem_Speichern()
    {
        var record = Record();
        var project = new Project();
        project.Data.Add(record);
        var calculationCosts = new RecordingProjectCostStoreRepository();
        var synchronizationCosts = new RecordingProjectCostStoreRepository();
        var synchronizer = new RecordingCostFieldSynchronizer();
        var dashboardRefresh = new DashboardRefreshNotifier();
        var refreshCount = 0;
        dashboardRefresh.CostsChanged += (_, _) => refreshCount++;
        var appliedCosts = new List<HoldingCost>();
        var settings = new AppSettings { LastProjectPath = @"C:\Projekte\Test\projekt.json" };
        var factory = CreateFactory(
            settings,
            calculationCosts,
            synchronizationCosts,
            synchronizer,
            dashboardRefresh);

        var viewModel = factory.Create(
            project,
            new DataPageSanierungWindowRequest(
                record,
                "H-01",
                InitialFocusMode.CostCalculator,
                Array.Empty<string>(),
                null,
                null,
                appliedCosts.Add,
                () => { }));

        Assert.Null(viewModel.OptimizationVm);
        Assert.Equal(InitialFocusMode.CostCalculator, viewModel.InitialFocus);

        viewModel.CostCalcVm.SaveCommand.Execute(null);

        Assert.Single(appliedCosts);
        Assert.Equal(1, calculationCosts.SaveCount);
        Assert.Same(project, synchronizer.Project);
        Assert.Same(synchronizationCosts.Store, synchronizer.Store);
        Assert.Equal(settings.LastProjectPath, synchronizationCosts.LastLoadedProjectPath);
        Assert.Equal(1, refreshCount);
    }

    [Fact]
    public async Task Create_verdrahtet_KI_Fabrik_Sitzungsspeicher_und_Transfer_Callback()
    {
        var record = Record();
        var project = new Project();
        project.Data.Add(record);
        var runtimeSettings = Settings();
        var optimizationFactory = new RecordingOptimizationFactory();
        var sessionStore = new RecordingOptimizationSessionStore();
        var transferCount = 0;
        var factory = CreateFactory(
            new AppSettings(),
            new RecordingProjectCostStoreRepository(),
            new RecordingProjectCostStoreRepository(),
            new RecordingCostFieldSynchronizer(),
            new DashboardRefreshNotifier(),
            optimizationFactory,
            sessionStore);

        var viewModel = factory.Create(
            project,
            new DataPageSanierungWindowRequest(
                record,
                "H-01",
                InitialFocusMode.AiOptimization,
                Array.Empty<string>(),
                runtimeSettings,
                null,
                _ => { },
                () => transferCount++));

        var optimization = Assert.IsType<SanierungOptimizationViewModel>(viewModel.OptimizationVm);
        Assert.Same(runtimeSettings, optimizationFactory.Settings);

        await optimization.OptimizeCommand.ExecuteAsync(null);
        await optimization.TransferToPrimaryCommand.ExecuteAsync(null);

        Assert.Equal(1, transferCount);
        Assert.Single(sessionStore.Saved);
    }

    private static DataPageSanierungViewModelFactory CreateFactory(
        AppSettings settings,
        RecordingProjectCostStoreRepository calculationCosts,
        RecordingProjectCostStoreRepository synchronizationCosts,
        RecordingCostFieldSynchronizer synchronizer,
        DashboardRefreshNotifier dashboardRefresh,
        IAiSanierungOptimizationFactory? optimizationFactory = null,
        IAiOptimizationSessionStore? sessionStore = null)
        => new(
            settings,
            new TestCostStoreFactory(calculationCosts),
            synchronizationCosts,
            synchronizer,
            dashboardRefresh,
            optimizationFactory ?? new RecordingOptimizationFactory(),
            sessionStore ?? new RecordingOptimizationSessionStore(),
            new AuswertungPro.Next.Infrastructure.Output.Offers.OfferPdfExportService());

    private static HaltungRecord Record()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "H-01", FieldSource.Manual, userEdited: false);
        return record;
    }

    private static AiRuntimeSettings Settings()
        => new(
            true,
            new Uri("http://localhost:11434"),
            "vision",
            "text",
            null,
            null,
            TimeSpan.FromSeconds(30),
            "5m",
            4096);

    private sealed class TestCostStoreFactory(IProjectCostStoreRepository projectCosts)
        : ICostStoreFactory
    {
        private readonly ICostCatalogStore _catalog = new EmptyCostCatalogStore();
        private readonly IMeasureTemplateStore _templates = new EmptyMeasureTemplateStore();

        public IProjectCostStoreRepository CreateProjectCostStore(string fileName = "costs.json")
            => projectCosts;

        public ICostCatalogStore CreateCostCatalogStore(string? userOverridePath = null)
            => _catalog;

        public IMeasureTemplateStore CreateMeasureTemplateStore(string? userOverridePath = null)
            => _templates;

        public IPositionTemplateStore CreatePositionTemplateStore(string? userOverridePath = null)
            => throw new NotSupportedException();

        public CostCalculationStores CreateCalculationStores(string projectCostFileName = "costs.json")
            => new(_catalog, _templates, projectCosts);
    }

    private sealed class EmptyCostCatalogStore : ICostCatalogStore
    {
        public string? LastUserOverrideLoadError => null;

        public CostCatalog LoadMerged(string? projectPath) => new();
        public CostCatalog LoadDefault(string? projectPath) => new();
        public CostCatalog LoadUserOverrides() => new();

        public bool SaveUserOverrides(CostCatalog catalog, out string error)
            => throw new NotSupportedException();

        public bool SaveUserOverrides(CostCatalog catalog, string? projectPath, out string error)
            => throw new NotSupportedException();

        public bool ResetUserOverrides(out string error)
            => throw new NotSupportedException();

        public CostCatalogItem? FindByPosition(CostCatalog catalog, string position) => null;

        public bool UpsertByPosition(
            CostCatalog catalog,
            string position,
            decimal? unitPrice,
            string? unit,
            bool active,
            IEnumerable<string>? aliases)
            => throw new NotSupportedException();
    }

    private sealed class EmptyMeasureTemplateStore : IMeasureTemplateStore
    {
        public string? LastUserOverrideLoadError => null;

        public MeasureTemplateCatalog LoadMerged(string? projectPath) => new();
        public MeasureTemplateCatalog LoadDefault(string? projectPath) => new();
        public MeasureTemplateCatalog LoadUserOverrides() => new();

        public bool SaveUserOverrides(MeasureTemplateCatalog catalog, out string error)
            => throw new NotSupportedException();

        public bool ResetUserOverrides(out string error)
            => throw new NotSupportedException();

        public bool UpsertUserTemplate(MeasureTemplate template, out string error)
            => throw new NotSupportedException();

        public bool DisableUserTemplate(string idOrName, out string error)
            => throw new NotSupportedException();

        public bool DeleteUserTemplate(string idOrName, out string error)
            => throw new NotSupportedException();
    }

    private sealed class RecordingProjectCostStoreRepository : IProjectCostStoreRepository
    {
        public ProjectCostStore Store { get; } = new();
        public string? LastLoadedProjectPath { get; private set; }
        public int SaveCount { get; private set; }

        public ProjectCostStore Load(string? projectPath)
        {
            LastLoadedProjectPath = projectPath;
            return Store;
        }

        public ProjectCostStore Load(string? projectPath, out string? loadError)
        {
            loadError = null;
            return Load(projectPath);
        }

        public bool Save(string? projectPath, ProjectCostStore store, out string? error)
        {
            SaveCount++;
            error = null;
            return true;
        }

        public string GetStorePath(string projectDirectory)
            => Path.Combine(projectDirectory, "costs.json");
    }

    private sealed class RecordingCostFieldSynchronizer : IDerivedCostFieldSynchronizer
    {
        public Project? Project { get; private set; }
        public ProjectCostStore? Store { get; private set; }

        public int Sync(Project project, ProjectCostStore store)
        {
            Project = project;
            Store = store;
            return 0;
        }
    }

    private sealed class RecordingOptimizationFactory : IAiSanierungOptimizationFactory
    {
        public AiRuntimeSettings? Settings { get; private set; }

        public IAiSanierungOptimizationService Create(
            AiRuntimeSettings settings,
            HttpClient? httpClient = null)
        {
            Settings = settings;
            return new SuccessfulOptimizationService();
        }
    }

    private sealed class SuccessfulOptimizationService : IAiSanierungOptimizationService
    {
        public Task<SanierungOptimizationResult> OptimizeAsync(
            SanierungOptimizationRequest req,
            CancellationToken ct)
            => Task.FromResult(new SanierungOptimizationResult
            {
                RecommendedMeasure = "Kurzliner",
                CostEstimate = new CostBand()
            });
    }

    private sealed class RecordingOptimizationSessionStore : IAiOptimizationSessionStore
    {
        public string StoragePath => "test.json";
        public List<AiOptimizationSession> Saved { get; } = new();

        public Task SaveAsync(AiOptimizationSession session)
        {
            Saved.Add(session);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AiOptimizationSession>> LoadAllAsync()
            => Task.FromResult<IReadOnlyList<AiOptimizationSession>>(Saved);

        public Task<IReadOnlyList<AiOptimizationSession>> LoadForHaltungAsync(string haltungId)
            => Task.FromResult<IReadOnlyList<AiOptimizationSession>>(
                Saved.Where(item => string.Equals(
                    item.HaltungId,
                    haltungId,
                    StringComparison.OrdinalIgnoreCase)).ToList());
    }
}
