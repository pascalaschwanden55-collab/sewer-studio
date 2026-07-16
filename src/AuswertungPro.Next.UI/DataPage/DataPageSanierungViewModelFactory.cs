using AuswertungPro.Next.Application.Ai.Sanierung;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.DataPage;

internal interface IDataPageSanierungViewModelFactory
{
    SanierungsmassnahmenViewModel Create(
        Project project,
        DataPageSanierungWindowRequest request);
}

/// <summary>
/// Baut die zusammengehoerenden Sanierungs-ViewModels und haelt deren
/// Speicher- und KI-Abhaengigkeiten aus dem DataPageViewModel heraus.
/// </summary>
internal sealed class DataPageSanierungViewModelFactory : IDataPageSanierungViewModelFactory
{
    private readonly AppSettings _settings;
    private readonly ICostStoreFactory _costStores;
    private readonly IProjectCostStoreRepository _projectCosts;
    private readonly IDerivedCostFieldSynchronizer _costFieldSynchronizer;
    private readonly DashboardRefreshNotifier _dashboardRefresh;
    private readonly IAiSanierungOptimizationFactory _sanierungOptimizations;
    private readonly IAiOptimizationSessionStore _optimizationSessions;

    public DataPageSanierungViewModelFactory(
        AppSettings settings,
        ICostStoreFactory costStores,
        IProjectCostStoreRepository projectCosts,
        IDerivedCostFieldSynchronizer costFieldSynchronizer,
        DashboardRefreshNotifier dashboardRefresh,
        IAiSanierungOptimizationFactory sanierungOptimizations,
        IAiOptimizationSessionStore optimizationSessions)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _costStores = costStores ?? throw new ArgumentNullException(nameof(costStores));
        _projectCosts = projectCosts ?? throw new ArgumentNullException(nameof(projectCosts));
        _costFieldSynchronizer = costFieldSynchronizer
            ?? throw new ArgumentNullException(nameof(costFieldSynchronizer));
        _dashboardRefresh = dashboardRefresh ?? throw new ArgumentNullException(nameof(dashboardRefresh));
        _sanierungOptimizations = sanierungOptimizations
            ?? throw new ArgumentNullException(nameof(sanierungOptimizations));
        _optimizationSessions = optimizationSessions
            ?? throw new ArgumentNullException(nameof(optimizationSessions));
    }

    public SanierungsmassnahmenViewModel Create(
        Project project,
        DataPageSanierungWindowRequest request)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(request);

        var costCalculator = new CostCalculatorViewModel(
            request.Holding,
            null,
            request.RecommendedTemplates,
            _settings.LastProjectPath,
            _costStores.CreateCalculationStores(),
            request.ApplyCosts,
            haltungRecord: request.Record,
            projectRecords: project.Data);

        costCalculator.Saved += () => SynchronizeProjectCosts(project);

        SanierungOptimizationViewModel? optimization = null;
        if (request.RuntimeSettings is not null)
        {
            optimization = new SanierungOptimizationViewModel(
                request.Record,
                _sanierungOptimizations.Create(request.RuntimeSettings),
                request.RuleRecommendation,
                _optimizationSessions);
            optimization.TransferredToPrimary += _ => request.OnOptimizationTransferred();
        }

        return new SanierungsmassnahmenViewModel(
            costCalculator,
            optimization,
            request.Record,
            request.Focus);
    }

    private void SynchronizeProjectCosts(Project project)
    {
        var projectPath = _settings.LastProjectPath;
        if (string.IsNullOrWhiteSpace(projectPath))
            return;

        var store = _projectCosts.Load(projectPath, out var loadError);
        if (loadError is not null)
            return;

        _costFieldSynchronizer.Sync(project, store);
        _dashboardRefresh.NotifyCostsChanged();
    }
}
