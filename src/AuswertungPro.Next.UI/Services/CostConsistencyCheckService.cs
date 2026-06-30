using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Cost;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// UI-seitige Fassade zum Konsistenz-Regelwerk KK01–KK14.
/// Behaelt die oeffentliche API (MeasureBlockVm / CostLineVm) bei,
/// konvertiert intern in IMeasureBlockView und delegiert an CostConsistencyChecker.
/// </summary>
public sealed class CostConsistencyCheckService
{
    private readonly CostConsistencyChecker _checker = new();

    public IReadOnlyList<ConsistencyWarning> CheckAll(
        IReadOnlyList<MeasureBlockVm> blocks,
        IReadOnlyDictionary<string, CostCatalogItem> catalog,
        IReadOnlyDictionary<string, MeasureTemplate> templates,
        ProjectCostStore? projectStore,
        string? currentHolding)
    {
        // VM → Read-Model konvertieren; der Checker ist VM-unabhaengig.
        var views = blocks.Select(ToView).ToList();
        return _checker.CheckAll(views, catalog, templates, projectStore, currentHolding);
    }

    // ---------------------------------------------------------------------------
    // Konvertierung MeasureBlockVm → IMeasureBlockView
    // ---------------------------------------------------------------------------

    private static IMeasureBlockView ToView(MeasureBlockVm block)
        => new MeasureBlockView
        {
            MeasureId = block.MeasureId,
            MeasureName = block.MeasureName,
            DnText = block.DnText,
            LengthText = block.LengthText,
            ConnectionsText = block.ConnectionsText,
            Total = block.Total,
            Lines = block.Lines.Select(ToView).ToList()
        };

    private static ICostLineView ToView(CostLineVm line)
        => new CostLineView
        {
            ItemKey = line.ItemKey,
            Text = line.Text,
            Unit = line.Unit,
            Qty = line.Qty,
            UnitPrice = line.UnitPrice,
            Selected = line.Selected,
            PriceMissing = line.PriceMissing,
            IsPriceOverridden = line.IsPriceOverridden
        };
}
