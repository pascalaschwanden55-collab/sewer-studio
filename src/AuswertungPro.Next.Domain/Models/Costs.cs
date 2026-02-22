using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Domain.Models;

public sealed record CostCatalog
{
    public int Version { get; set; } = 1;
    public string Currency { get; set; } = "CHF";
    public decimal VatRate { get; set; }
    public List<CostCatalogItem> Items { get; set; } = new();
}

public sealed record CostCatalogItem
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public string Type { get; set; } = "Fixed";
    public decimal? Price { get; set; }
    public List<DnPrice> DnPrices { get; set; } = new();
    public bool Active { get; set; } = true;
    public List<string> Aliases { get; set; } = new();
}

public sealed record DnPrice
{
    public int DnFrom { get; set; }
    public int DnTo { get; set; }
    public decimal? QtyFrom { get; set; }
    public decimal? QtyTo { get; set; }
    public decimal Price { get; set; }
}

public sealed record MeasureTemplateCatalog
{
    public int Version { get; set; } = 1;
    public List<MeasureTemplate> Measures { get; set; } = new();
}

public sealed record MeasureTemplate
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<MeasureLineTemplate> Lines { get; set; } = new();
    public bool Disabled { get; set; }
}

public sealed record MeasureLineTemplate
{
    public string Group { get; set; } = "";
    public string ItemKey { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public decimal DefaultQty { get; set; }
}

public sealed record CostLine
{
    public string Group { get; set; } = "";
    public string ItemKey { get; set; } = "";
    public string Text { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public bool Selected { get; set; }
    public bool TransferMarked { get; set; }
    public bool IsPriceOverridden { get; set; }
    public bool IsQtyOverridden { get; set; }
}

public sealed record MeasureCost
{
    public string MeasureId { get; set; } = "";
    public string MeasureName { get; set; } = "";
    public int? Dn { get; set; }
    public decimal? LengthMeters { get; set; }
    public List<CostLine> Lines { get; set; } = new();
    public decimal Total { get; set; }
}

public sealed record HoldingCost
{
    public string Holding { get; set; } = "";
    public DateTime? Date { get; set; }
    public List<MeasureCost> Measures { get; set; } = new();
    public decimal Total { get; set; }
    public decimal MwstRate { get; set; }
    public decimal MwstAmount { get; set; }
    public decimal TotalInclMwst { get; set; }
}

public sealed record ProjectCostStore
{
    public Dictionary<string, HoldingCost> ByHolding { get; set; } = new();
}

public sealed record PositionTemplateCatalog
{
    public int Version { get; set; } = 1;
    public List<PositionGroup> Groups { get; set; } = new();
}

public sealed record PositionGroup
{
    public string Name { get; set; } = "";
    public List<PositionTemplate> Positions { get; set; } = new();
}

public sealed record PositionTemplate
{
    public string ItemKey { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public decimal DefaultQty { get; set; }

    // Für freie Eingabe
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal Price { get; set; }
    public bool IsCustom { get; set; } = true;
}
