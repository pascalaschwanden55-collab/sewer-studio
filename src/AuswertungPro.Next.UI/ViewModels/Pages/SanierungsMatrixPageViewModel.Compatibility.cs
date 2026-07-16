using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class SanierungsMatrixPageViewModel
{
    [Obsolete("Uebergangskonstruktor. Neue Aufrufer sollen die Kosten-Speicher injizieren.")]
    public SanierungsMatrixPageViewModel(
        ShellViewModel shell,
        AppSettings settings,
        IDialogService dialogs,
        IDerivedCostFieldSynchronizer costFieldSync,
        DashboardRefreshNotifier dashboardRefresh,
        string? holding,
        bool singleHoldingMode,
        HaltungRecord? targetRecord = null)
        : this(
            shell,
            settings,
            dialogs,
            costFieldSync,
            dashboardRefresh,
            CostStoreCompatibility.Factory.CreateCostCatalogStore(),
            CostStoreCompatibility.Factory.CreateMeasureTemplateStore(),
            CostStoreCompatibility.Factory.CreateProjectCostStore(),
            holding,
            singleHoldingMode,
            targetRecord)
    {
    }
}
