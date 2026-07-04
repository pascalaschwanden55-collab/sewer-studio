using System.Collections.Generic;

namespace AuswertungPro.Next.Infrastructure.Output.Offers;

public sealed class NpkOfferPdfModel
{
    public string LogoDataUri { get; set; } = "";
    public string DocumentKindLabel { get; set; } = "NPK-135-Offerte";
    public string OfferNo { get; set; } = "";
    public string DateText { get; set; } = "";
    public string ValidityText { get; set; } = "";

    public string SenderBlock { get; set; } = "";
    public string CustomerBlock { get; set; } = "";
    public string ObjectBlock { get; set; } = "";
    public string ReferenceBlock { get; set; } = "";

    public string ProjectTitle { get; set; } = "";
    public string VariantTitle { get; set; } = "";
    public string FilterSummaryText { get; set; } = "";

    public List<string> IntroBlocks { get; set; } = new();
    public List<NpkOfferChapterSummaryLineModel> ChapterSummaryLines { get; set; } = new();
    public List<NpkOfferPositionLineModel> PositionLines { get; set; } = new();
    public List<NpkOfferConditionLineModel> ConditionLines { get; set; } = new();
    public List<string> Footnotes { get; set; } = new();
    public NpkOfferTotalsModel Totals { get; set; } = new();
}

public sealed class NpkOfferChapterSummaryLineModel
{
    public string Chapter { get; set; } = "";
    public string Title { get; set; } = "";
    public string TotalText { get; set; } = "";
}

public sealed class NpkOfferPositionLineModel
{
    public string ChapterTitle { get; set; } = "";
    public string NpkCode { get; set; } = "";
    public string Text { get; set; } = "";
    public string DnText { get; set; } = "";
    public string QtyText { get; set; } = "";
    public string Unit { get; set; } = "";
    public string UnitPriceText { get; set; } = "";
    public string TotalText { get; set; } = "";
    public string HoldingCountText { get; set; } = "";
}

public sealed class NpkOfferConditionLineModel
{
    public string Label { get; set; } = "";
    public string ValueText { get; set; } = "";
}

public sealed class NpkOfferTotalsModel
{
    public string GrossNetText { get; set; } = "";
    public string DiscountText { get; set; } = "";
    public string SkontoText { get; set; } = "";
    public string NetText { get; set; } = "";
    public string VatText { get; set; } = "";
    public string TotalInclVatText { get; set; } = "";
}

public sealed class NpkOfferPdfContext
{
    public string ProjectTitle { get; set; } = "";
    public string VariantTitle { get; set; } = "";
    public string CustomerBlock { get; set; } = "";
    public string ObjectBlock { get; set; } = "";
    public string ReferenceBlock { get; set; } = "";
    public string OfferNo { get; set; } = "";
    public string ValidityText { get; set; } = "";
    public string FilterSummaryText { get; set; } = "";
    public string Currency { get; set; } = "CHF";
    public decimal VatRate { get; set; } = 0.081m;
    public decimal DiscountPercent { get; set; }
    public decimal SkontoPercent { get; set; }
    public List<string> IntroBlocks { get; set; } = new();
    public List<NpkOfferConditionLineModel> ConditionLines { get; set; } = new();
}
