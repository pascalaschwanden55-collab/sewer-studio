namespace AuswertungPro.Next.Domain.Models.Costs;

public sealed class OfferLine
{
    public string Measure { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? Amount { get; set; }
    public string? Source { get; set; }
}
