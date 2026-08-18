using System.Collections.Generic;
using AuswertungPro.Next.Application.Output;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Output.Offers;

public sealed class OfferPdfModel : IOfferPdfModel
{
    public string LogoDataUri { get; set; } = "";
    public string DocumentKindLabel { get; set; } = "Offerte";

    public string OfferNo { get; set; } = "";
    public string DateText { get; set; } = "";
    public string ValidityText { get; set; } = "";

    public string SenderBlock { get; set; } = "";
    public string CustomerBlock { get; set; } = "";
    public string ObjectBlock { get; set; } = "";

    public string ProjectTitle { get; set; } = "";
    public string VariantTitle { get; set; } = "";

    public List<OfferPdfLineModel> Lines { get; set; } = new();
    public List<OfferPdfHoldingDataLineModel> HoldingDataLines { get; set; } = new();
    public List<OfferPdfMeasureSummaryLineModel> MeasureSummaryLines { get; set; } = new();
    public List<OfferPdfSpecialStatsLineModel> SpecialStatsLines { get; set; } = new();
    public List<OfferPdfOwnerSummaryLineModel> OwnerSummaryLines { get; set; } = new();
    public List<OfferPdfExecutorCostChartLineModel> ExecutorCostChartLines { get; set; } = new();
    public List<OfferPdfPositionSummaryLineModel> PositionSummaryLines { get; set; } = new();

    /// <summary>Detailliste je Bauteil, nach Bauteilart gruppiert.</summary>
    public List<OfferPdfDetailGroupModel> DetailGroups { get; set; } = new();

    public OfferPdfTotalsModel Totals { get; set; } = new();

    public string FilterSummaryText { get; set; } = "";
    public List<string> TextBlocks { get; set; } = new();
}

public sealed class OfferPdfLineModel
{
    public string GroupLabel { get; set; } = "";
    public string Text { get; set; } = "";
    public string Note { get; set; } = "";

    public string QtyText { get; set; } = "";
    public string Unit { get; set; } = "";
    public string UnitPriceText { get; set; } = "";
    public string TotalText { get; set; } = "";
}

/// <summary>
/// Totale des Ausdrucks. <see cref="OfferPdfTotalsModel.EntryCount"/> speist den
/// Kennzahlenstreifen auf Seite 1.
/// </summary>
public sealed class OfferPdfTotalsModel
{
    public string NetText { get; set; } = "";
    public string VatText { get; set; } = "";
    public string GrossText { get; set; } = "";

    /// <summary>Anzahl Bauteile mit Kosten im Ausdruck — fuer den Kennzahlenstreifen.</summary>
    public int EntryCount { get; set; }

    /// <summary>Bezeichnung dazu ("Haltungen"/"Schächte").</summary>
    public string EntryCountLabel { get; set; } = "";
}

public sealed class OfferPdfMeasureSummaryLineModel
{
    public string MeasureName { get; set; } = "";
    public string HoldingCountText { get; set; } = "";
    public string NetText { get; set; } = "";
}

public sealed class OfferPdfHoldingDataLineModel
{
    public string Holding { get; set; } = "";
    public string Street { get; set; } = "";
    public string Owner { get; set; } = "";
    public string ExecutedBy { get; set; } = "";
    public string Sanieren { get; set; } = "";
    public string Material { get; set; } = "";
    public string Zustand { get; set; } = "";
    public string NetText { get; set; } = "";
    public string DetailText { get; set; } = "";
    public string MeasuresText { get; set; } = "";
}

public sealed class OfferPdfSpecialStatsLineModel
{
    public string Category { get; set; } = "";
    public string QtyText { get; set; } = "";
    public string Unit { get; set; } = "";
    public string NetText { get; set; } = "";
}

public sealed class OfferPdfOwnerSummaryLineModel
{
    public string Owner { get; set; } = "";
    public string HoldingCountText { get; set; } = "";
    public string NetText { get; set; } = "";
    public string VatText { get; set; } = "";
    public string GrossText { get; set; } = "";
}

public sealed class OfferPdfExecutorCostChartLineModel
{
    public string Executor { get; set; } = "";
    public string HoldingCountText { get; set; } = "";
    public string NetText { get; set; } = "";
    public string ShareText { get; set; } = "";
    public string BarWidthPercentText { get; set; } = "";
}

public sealed class OfferPdfPositionSummaryLineModel
{
    public string GroupLabel { get; set; } = "";
    public string Position { get; set; } = "";
    public string QtyText { get; set; } = "";
    public string Unit { get; set; } = "";
    public string UnitPriceText { get; set; } = "";
    public string TotalText { get; set; } = "";
    public string HoldingCountText { get; set; } = "";
}

public sealed class CostSummaryEntry
{
    public string Holding { get; set; } = "";
    public string Owner { get; set; } = "";
    public string ExecutedBy { get; set; } = "";
    public HoldingCost Cost { get; set; } = new();

    /// <summary>
    /// Bauteilart fuer die Detailliste ("Haltungen"/"Schächte"). Leer = einbereichiger
    /// Ausdruck, dann entsteht genau eine Gruppe.
    /// </summary>
    public string GroupLabel { get; set; } = "";

    /// <summary>Strasse/Lage — nur Anzeige in der Kopfzeile der Detailliste.</summary>
    public string Street { get; set; } = "";
}

/// <summary>Ein Abschnitt der Detailliste: alle Bauteile einer Art mit Zwischentotal.</summary>
public sealed class OfferPdfDetailGroupModel
{
    public string Title { get; set; } = "";
    public string EntryCountText { get; set; } = "";
    public string SubtotalText { get; set; } = "";
    public List<OfferPdfDetailEntryModel> Entries { get; set; } = new();
}

/// <summary>Ein Bauteil in der Detailliste: Kopfzeile plus seine Massnahmen.</summary>
public sealed class OfferPdfDetailEntryModel
{
    public string Name { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Street { get; set; } = "";
    public string TotalText { get; set; } = "";
    public List<OfferPdfDetailMeasureModel> Measures { get; set; } = new();
}

/// <summary>Eine Massnahme in der Detailliste.</summary>
public sealed class OfferPdfDetailMeasureModel
{
    public string Name { get; set; } = "";

    /// <summary>Menge samt Einheit — leer, wenn die Einheiten uneinheitlich sind.</summary>
    public string QtyText { get; set; } = "";

    public string TotalText { get; set; } = "";
}

public sealed class OfferPdfContext
{
    public string ProjectTitle { get; set; } = "";
    public string VariantTitle { get; set; } = "";

    public string CustomerBlock { get; set; } = "";
    public string ObjectBlock { get; set; } = "";

    public string OfferNo { get; set; } = "";
    public string ValidityText { get; set; } = "";
    public string FilterSummaryText { get; set; } = "";

    public string Currency { get; set; } = "CHF";

    public List<string> TextBlocks { get; set; } = new();
}
