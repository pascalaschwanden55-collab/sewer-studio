using System.Collections.Generic;

namespace AuswertungPro.Next.Domain.Models.Costs;

public sealed class CalculatedOffer
{
    public string TemplateId { get; set; } = string.Empty;
    public List<OfferLine> Lines { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public OfferTotals Totals { get; set; } = new();
}
