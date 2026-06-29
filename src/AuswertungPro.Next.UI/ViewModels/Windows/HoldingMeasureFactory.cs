using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;
using InfraFactory = AuswertungPro.Next.Infrastructure.Costs.HoldingMeasureFactory;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

/// <summary>
/// Thin-Forwarder auf <see cref="Infrastructure.Costs.HoldingMeasureFactory"/>.
/// Die eigentliche Logik liegt jetzt headless in Infrastructure.Costs.
/// Diese Klasse bleibt fuer Rueckwaertskompatibilitaet erhalten.
/// </summary>
public static class HoldingMeasureFactory
{
    public static HoldingCost? Build(
        string holdingName,
        HaltungRecord? record,
        string measureId,
        IReadOnlyDictionary<string, MeasureTemplate> templates,
        IReadOnlyDictionary<string, CostCatalogItem> catalog,
        decimal vatRate,
        IReadOnlyCollection<string>? extraOptionKeys = null,
        decimal? hauptarbeitMenge = null,
        string? hauptarbeitItemKey = null)
        => InfraFactory.Build(
            holdingName, record, measureId,
            templates, catalog, vatRate,
            extraOptionKeys, hauptarbeitMenge, hauptarbeitItemKey);
}
