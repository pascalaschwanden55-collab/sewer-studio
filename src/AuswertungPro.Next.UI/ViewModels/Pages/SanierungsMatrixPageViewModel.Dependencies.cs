using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class SanierungsMatrixPageViewModel
{
    public SanierungsMatrixPageViewModel(
        ShellViewModel shell,
        AppSettings settings,
        IDialogService dialogs,
        IDerivedCostFieldSynchronizer costFieldSync,
        DashboardRefreshNotifier dashboardRefresh,
        CostCalculationStores costStores,
        string? holding,
        bool singleHoldingMode,
        HaltungRecord? targetRecord = null)
        : this(
            shell,
            settings,
            dialogs,
            costFieldSync,
            dashboardRefresh,
            costStores?.Catalog ?? throw new ArgumentNullException(nameof(costStores)),
            costStores.Templates,
            costStores.ProjectCosts,
            holding,
            singleHoldingMode,
            targetRecord)
    {
    }
}
