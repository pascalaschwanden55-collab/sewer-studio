namespace AuswertungPro.Next.Application.Reports;

/// <summary>Abgeflachtes Hydraulik-Ergebnis fuer Ausgabewege.</summary>
public sealed record HydraulikCalcResult
{
    public double DN_mm { get; init; }
    public double Wasserstand_mm { get; init; }
    public double Gefaelle_Promille { get; init; }
    public double Kb { get; init; }
    public string AbwasserTyp { get; init; } = "MR";
    public double Temperatur_C { get; init; }
    public string Material { get; init; } = "";
    public double V_T { get; init; }
    public double Q_T { get; init; }
    public double A_T { get; init; }
    public double Lu_T { get; init; }
    public double Rhy_T { get; init; }
    public double Bsp { get; init; }
    public double V_V { get; init; }
    public double Q_V { get; init; }
    public double Re { get; init; }
    public double Fr { get; init; }
    public double Lambda { get; init; }
    public double Tau { get; init; }
    public double Ny { get; init; }
    public double Vc { get; init; }
    public double Ic { get; init; }
    public double TauC { get; init; }
    public double Auslastung { get; init; }
    public bool VelocityOk { get; init; }
    public bool ShearOk { get; init; }
    public bool FroudeOk { get; init; }
    public bool AblagerungOk { get; init; }
}
