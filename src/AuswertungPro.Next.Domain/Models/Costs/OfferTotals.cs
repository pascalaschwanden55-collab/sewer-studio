namespace AuswertungPro.Next.Domain.Models.Costs;

public sealed class OfferTotals
{
    public decimal SubTotal { get; set; }
    public decimal RabattPct { get; set; }
    public decimal Rabatt { get; set; }
    public decimal SkontoPct { get; set; }
    public decimal Skonto { get; set; }
    public decimal NetExclMwst { get; set; }
    public decimal MwstPct { get; set; }
    public decimal Mwst { get; set; }
    public decimal TotalInclMwst { get; set; }
    public string Currency { get; set; } = "CHF";
}
