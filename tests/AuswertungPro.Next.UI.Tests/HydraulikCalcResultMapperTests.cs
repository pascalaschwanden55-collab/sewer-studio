using AuswertungPro.Next.Application.Hydraulik;

namespace AuswertungPro.Next.UI.Tests;

public sealed class HydraulikCalcResultMapperTests
{
    [Fact]
    public void ToReportResult_maps_input_result_and_material()
    {
        var input = new HydraulikInput(
            DN_mm: 300,
            Wasserstand_mm: 150,
            Gefaelle_Promille: 12,
            Kb: 0.001,
            AbwasserTyp: "MR",
            Temperatur_C: 12);
        var result = HydraulikEngine.Berechne(input)!;

        var mapped = HydraulikCalcResultMapper.ToReportResult(input, result, "Beton");

        Assert.Equal(300, mapped.DN_mm);
        Assert.Equal(150, mapped.Wasserstand_mm);
        Assert.Equal(12, mapped.Gefaelle_Promille);
        Assert.Equal(0.001, mapped.Kb);
        Assert.Equal("MR", mapped.AbwasserTyp);
        Assert.Equal(12, mapped.Temperatur_C);
        Assert.Equal("Beton", mapped.Material);
        Assert.Equal(result.V_T, mapped.V_T);
        Assert.Equal(result.Q_T, mapped.Q_T);
        Assert.Equal(result.A_T, mapped.A_T);
        Assert.Equal(result.Lu_T, mapped.Lu_T);
        Assert.Equal(result.Rhy_T, mapped.Rhy_T);
        Assert.Equal(result.Bsp, mapped.Bsp);
        Assert.Equal(result.V_V, mapped.V_V);
        Assert.Equal(result.Q_V, mapped.Q_V);
        Assert.Equal(result.Re, mapped.Re);
        Assert.Equal(result.Fr, mapped.Fr);
        Assert.Equal(result.Lambda, mapped.Lambda);
        Assert.Equal(result.Tau, mapped.Tau);
        Assert.Equal(result.Ny, mapped.Ny);
        Assert.Equal(result.Abl.Vc, mapped.Vc);
        Assert.Equal(result.Abl.Ic, mapped.Ic);
        Assert.Equal(result.Abl.TauC, mapped.TauC);
        Assert.Equal(result.Auslastung, mapped.Auslastung);
        Assert.Equal(result.VelocityOk, mapped.VelocityOk);
        Assert.Equal(result.ShearOk, mapped.ShearOk);
        Assert.Equal(result.Fr <= 1, mapped.FroudeOk);
        Assert.Equal(result.AblagerungOk, mapped.AblagerungOk);
    }
}
