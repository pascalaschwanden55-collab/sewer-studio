using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Output.Offers;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public static class BuilderPageHoldingDataLineBuilder
{
    public static List<OfferPdfHoldingDataLineModel> Build(IReadOnlyList<DruckcenterRowVm> rows)
        => rows
            .OrderBy(row => string.IsNullOrWhiteSpace(row.ExecutedBy) ? 1 : 0)
            .ThenBy(row => row.ExecutedBy, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Owner, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Holding, StringComparer.OrdinalIgnoreCase)
            .Select(row => new OfferPdfHoldingDataLineModel
            {
                Holding = row.Holding,
                Street = row.Street,
                Owner = row.Owner,
                ExecutedBy = row.ExecutedBy,
                Sanieren = row.Sanieren,
                Material = row.Material,
                Zustand = row.Zustand,
                NetText = ChfFormat.Money(row.NetCost),
                DetailText = row.CostSource,
                MeasuresText = row.MeasuresPreview
            })
            .ToList();
}
