using System.Collections.Generic;

namespace AuswertungPro.Next.Domain.Models.Costs;

public sealed class MeasureInputs
{
    public int Dn { get; set; }
    public decimal LengthM { get; set; }
    public int Connections { get; set; }
    public int EndCuffs { get; set; } = 2;
    public bool Waterholding { get; set; }
    public decimal RabattPct { get; set; }
    public decimal SkontoPct { get; set; }
    public decimal MwstPct { get; set; } = 8.1m;

    public Dictionary<string, object> ToDictionary()
    {
        return new Dictionary<string, object>
        {
            ["dn"] = Dn,
            ["length_m"] = LengthM,
            ["connections"] = Connections,
            ["end_cuffs"] = EndCuffs,
            ["waterholding"] = Waterholding,
            ["rabatt_pct"] = RabattPct,
            ["skonto_pct"] = SkontoPct,
            ["mwst_pct"] = MwstPct
        };
    }
}
