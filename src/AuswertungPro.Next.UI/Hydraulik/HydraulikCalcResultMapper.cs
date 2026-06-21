using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.UI.Hydraulik;

public static class HydraulikCalcResultMapper
{
    public static HydraulikCalcResult ToReportResult(
        HydraulikInput input,
        HydraulikResult result,
        string materialLabel)
        => new()
        {
            DN_mm = input.DN_mm,
            Wasserstand_mm = input.Wasserstand_mm,
            Gefaelle_Promille = input.Gefaelle_Promille,
            Kb = input.Kb,
            AbwasserTyp = input.AbwasserTyp,
            Temperatur_C = input.Temperatur_C,
            Material = materialLabel,
            V_T = result.V_T,
            Q_T = result.Q_T,
            A_T = result.A_T,
            Lu_T = result.Lu_T,
            Rhy_T = result.Rhy_T,
            Bsp = result.Bsp,
            V_V = result.V_V,
            Q_V = result.Q_V,
            Re = result.Re,
            Fr = result.Fr,
            Lambda = result.Lambda,
            Tau = result.Tau,
            Ny = result.Ny,
            Vc = result.Abl.Vc,
            Ic = result.Abl.Ic,
            TauC = result.Abl.TauC,
            Auslastung = result.Auslastung,
            VelocityOk = result.VelocityOk,
            ShearOk = result.ShearOk,
            FroudeOk = result.Fr <= 1,
            AblagerungOk = result.AblagerungOk,
        };
}
