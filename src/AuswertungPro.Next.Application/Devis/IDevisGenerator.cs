using AuswertungPro.Next.Domain.Models.Devis;

namespace AuswertungPro.Next.Application.Devis;

public interface IDevisGenerator
{
    DevisErgebnis Generate(string baustelle, string zone, List<HaltungMitSchaeden> haltungen);
}

public sealed class HaltungMitSchaeden
{
    public string HaltungsId { get; set; } = "";
    public string VonSchacht { get; set; } = "";
    public string BisSchacht { get; set; } = "";
    public int DN { get; set; }
    public decimal Laenge { get; set; }
    public decimal Grabentiefe { get; set; } = 0.8m;
    public int AnzahlSchaechte { get; set; } = 2;
    public string Material { get; set; } = "";
    public int Zustandsklasse { get; set; }
    public List<SchadenInfo> Schaeden { get; set; } = [];
}

public sealed class SchadenInfo
{
    public string Code { get; set; } = "";
    public string? Char1 { get; set; }
    public string? Char2 { get; set; }
    public decimal? MeterStart { get; set; }
    public decimal? MeterEnd { get; set; }
    public int Zustandsklasse { get; set; }
    public double AiConfidence { get; set; }
}
