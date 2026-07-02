namespace AuswertungPro.Next.Application.Hydraulik;

public sealed class HydraulikPanelSettings
{
    public double Dn { get; set; } = 300;
    public string MaterialKey { get; set; } = "Beton";
    public bool IsNeuzustand { get; set; }
    public double Gefaelle { get; set; } = 5;
    public bool IsGefaellePercent { get; set; }
    public double Wasserstand { get; set; } = 90;
    public bool IsMischRegen { get; set; } = true;
    public double Temperatur { get; set; } = 10;
}
