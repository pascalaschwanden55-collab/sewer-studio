using System.Reflection;
using System.IO;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Dialogs;
using AuswertungPro.Next.UI.ViewModels.Pages;
using AuswertungPro.Next.UI.ViewModels.Windows;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CostStoreDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_die_zentrale_Kosten_Speicherfabrik()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.IsType<CostStoreFactory>(services.CostStores);
        Assert.Same(services.CostStores, services.GetService(typeof(ICostStoreFactory)));
    }

    [Fact]
    public void Fabrik_liefert_Application_Vertraege_und_frische_fehlermerkende_Stores()
    {
        ICostStoreFactory factory = new CostStoreFactory();

        var firstCatalog = factory.CreateCostCatalogStore();
        var secondCatalog = factory.CreateCostCatalogStore();

        Assert.IsAssignableFrom<ICostCatalogStore>(firstCatalog);
        Assert.IsAssignableFrom<IMeasureTemplateStore>(factory.CreateMeasureTemplateStore());
        Assert.IsAssignableFrom<IPositionTemplateStore>(factory.CreatePositionTemplateStore());
        Assert.IsAssignableFrom<IProjectCostStoreRepository>(factory.CreateProjectCostStore());
        Assert.NotSame(firstCatalog, secondCatalog);
    }

    [Fact]
    public void Fabrik_buendelt_zusammengehoerende_Kosten_Speicher_als_frische_Instanzen()
    {
        ICostStoreFactory factory = new CostStoreFactory();

        var first = factory.CreateCalculationStores("schacht_costs.json");
        var second = factory.CreateCalculationStores("schacht_costs.json");

        Assert.IsAssignableFrom<ICostCatalogStore>(first.Catalog);
        Assert.IsAssignableFrom<IMeasureTemplateStore>(first.Templates);
        Assert.IsAssignableFrom<IProjectCostStoreRepository>(first.ProjectCosts);
        Assert.EndsWith(
            Path.Combine("costs", "schacht_costs.json"),
            first.ProjectCosts.GetStorePath(Path.GetTempPath()));
        Assert.NotSame(first.Catalog, second.Catalog);
        Assert.NotSame(first.Templates, second.Templates);
        Assert.NotSame(first.ProjectCosts, second.ProjectCosts);
    }

    [Theory]
    [InlineData(typeof(CostCalculatorViewModel))]
    [InlineData(typeof(SanierungsMatrixPageViewModel))]
    [InlineData(typeof(SchachtSanierungsMatrixPageViewModel))]
    public void Kostenansichten_koennen_das_gebuendelte_Speicherpaket_entgegennehmen(Type viewModelType)
    {
        var acceptsBundle = viewModelType.GetConstructors()
            .Any(constructor => constructor.GetParameters()
                .Any(parameter => parameter.ParameterType == typeof(CostCalculationStores)));

        Assert.True(acceptsBundle, $"{viewModelType.Name} nimmt das Kosten-Speicherpaket nicht an.");
    }

    [Theory]
    [InlineData(typeof(BuilderPageViewModel), "_costRepo", typeof(IProjectCostStoreRepository))]
    [InlineData(typeof(BuilderPageViewModel), "_catalogStore", typeof(ICostCatalogStore))]
    [InlineData(typeof(DataPageViewModel), "_projectCosts", typeof(IProjectCostStoreRepository))]
    [InlineData(typeof(DataPageViewModel), "_measureTemplates", typeof(IMeasureTemplateStore))]
    [InlineData(typeof(DataPagePrintController), "_projectCosts", typeof(IProjectCostStoreRepository))]
    [InlineData(typeof(ExportPageViewModel), "_projectCosts", typeof(IProjectCostStoreRepository))]
    [InlineData(typeof(OverviewPageViewModel), "_haltungCostRepo", typeof(IProjectCostStoreRepository))]
    [InlineData(typeof(OverviewPageViewModel), "_schachtCostRepo", typeof(IProjectCostStoreRepository))]
    [InlineData(typeof(SanierungsMatrixPageViewModel), "_catalogStore", typeof(ICostCatalogStore))]
    [InlineData(typeof(SanierungsMatrixPageViewModel), "_templateStore", typeof(IMeasureTemplateStore))]
    [InlineData(typeof(SanierungsMatrixPageViewModel), "_costRepo", typeof(IProjectCostStoreRepository))]
    [InlineData(typeof(SchachtSanierungsMatrixPageViewModel), "_catalogStore", typeof(ICostCatalogStore))]
    [InlineData(typeof(SchachtSanierungsMatrixPageViewModel), "_templateStore", typeof(IMeasureTemplateStore))]
    [InlineData(typeof(SchachtSanierungsMatrixPageViewModel), "_costRepo", typeof(IProjectCostStoreRepository))]
    [InlineData(typeof(SchaechtePageViewModel), "_schachtRecommendationCosts", typeof(IProjectCostStoreRepository))]
    [InlineData(typeof(CostCalculatorViewModel), "_catalogStore", typeof(ICostCatalogStore))]
    [InlineData(typeof(CostCalculatorViewModel), "_templateStore", typeof(IMeasureTemplateStore))]
    [InlineData(typeof(CostCalculatorViewModel), "_costRepo", typeof(IProjectCostStoreRepository))]
    [InlineData(typeof(CostCatalogEditorViewModel), "_store", typeof(ICostCatalogStore))]
    [InlineData(typeof(MeasureTemplateEditorViewModel), "_templateStore", typeof(IMeasureTemplateStore))]
    [InlineData(typeof(MeasureTemplateEditorViewModel), "_catalogStore", typeof(ICostCatalogStore))]
    [InlineData(typeof(PositionTemplateEditorViewModel), "_store", typeof(IPositionTemplateStore))]
    [InlineData(typeof(PositionTemplateEditorViewModel), "_catalogStore", typeof(ICostCatalogStore))]
    public void ViewModels_halten_nur_Application_Vertraege(
        Type viewModelType,
        string fieldName,
        Type expectedType)
    {
        var field = viewModelType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.Equal(expectedType, field!.FieldType);
    }

    [Fact]
    public void Bestehende_oeffentliche_Konstruktoren_bleiben_als_Uebergang_erhalten()
    {
        AssertPublicConstructor(typeof(CostCatalogEditorDialog), "String");
        AssertPublicConstructor(typeof(PositionTemplateEditorDialog), "String");
        AssertPublicConstructor(
            typeof(ProjectPageViewModel),
            "ShellViewModel", "IDialogService", "IDropdownOptionsStore");
        AssertPublicConstructor(
            typeof(OverviewPageViewModel),
            "ShellViewModel", "AppSettings", "DashboardRefreshNotifier", "IDialogService", "IProjectRepository");
        AssertPublicConstructor(
            typeof(CostCalculatorViewModel),
            "String", "Nullable`1", "IReadOnlyList`1", "String", "Action`1", "HaltungRecord", "IReadOnlyList`1", "IDialogService", "IOfferPdfExportService");
        AssertPublicConstructor(
            typeof(MeasureTemplateEditorViewModel),
            "String", "IDialogService", "MeasureTemplateStore", "CostCatalogStore", "String", "String");
        AssertPublicConstructor(
            typeof(PositionTemplateEditorViewModel),
            "String", "Window", "IDialogService", "PositionTemplateStore", "CostCatalogStore");
    }

    private static void AssertPublicConstructor(Type type, params string[] parameterTypeNames)
    {
        var found = type.GetConstructors().Any(constructor =>
            constructor.GetParameters()
                .Select(parameter => parameter.ParameterType.Name)
                .SequenceEqual(parameterTypeNames));

        Assert.True(
            found,
            $"Der bisherige oeffentliche Konstruktor von {type.Name} fehlt: " +
            string.Join(", ", parameterTypeNames));
    }
}
