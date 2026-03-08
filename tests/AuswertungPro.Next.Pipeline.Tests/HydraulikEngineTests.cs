using System;
using AuswertungPro.Next.UI.Hydraulik;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class HydraulikEngineTests
{
    [Fact]
    public void KreisGeometrie_Halbfuellung_IstKonsistent()
    {
        const double d = 1.0;
        const double h = 0.5;

        var area = HydraulikEngine.KreisTeilflaeche(d, h);
        var wetted = HydraulikEngine.KreisTeilumfang(d, h);
        var topWidth = HydraulikEngine.KreisWasserspiegelbreite(d, h);

        Assert.InRange(area, (Math.PI / 8.0) - 1e-9, (Math.PI / 8.0) + 1e-9);
        Assert.InRange(wetted, (Math.PI / 2.0) - 1e-9, (Math.PI / 2.0) + 1e-9);
        Assert.InRange(topWidth, 1.0 - 1e-9, 1.0 + 1e-9);
    }

    [Fact]
    public void ReynoldsUndFroude_Berechnung_Stimmt()
    {
        var re = HydraulikEngine.ReynoldsZahl(v: 1.0, rhy: 0.1, ny: 1e-6);
        var fr = HydraulikEngine.FroudeZahl(v: 1.0, A: 0.05, bsp: 0.2);

        Assert.InRange(re, 400000 - 1, 400000 + 1);
        Assert.InRange(fr, 0.63, 0.65);
    }

    [Fact]
    public void Berechne_TeilsystemeSindPlausibelUndKonsistent()
    {
        var input = new HydraulikInput(
            DN_mm: 300,
            Wasserstand_mm: 150,
            Gefaelle_Promille: 5,
            Kb: 0.0005,
            AbwasserTyp: "MR",
            Temperatur_C: 10);

        var result = HydraulikEngine.Berechne(input);

        Assert.NotNull(result);
        Assert.True(double.IsFinite(result!.V_T));
        Assert.True(double.IsFinite(result.Q_T));
        Assert.True(double.IsFinite(result.A_T));
        Assert.True(result.A_T > 0);
        Assert.True(result.Lu_T > 0);
        Assert.InRange(Math.Abs(result.Q_T - (result.V_T * result.A_T)), 0, 1e-12);
        Assert.InRange(result.Auslastung, 0.4999, 0.5001);
    }

    [Fact]
    public void Ablagerungsgefahr_MRUndS_GebenUnterschiedlicheKritischeWerte()
    {
        var input = new HydraulikInput(
            DN_mm: 300,
            Wasserstand_mm: 120,
            Gefaelle_Promille: 5,
            Kb: 0.0005,
            AbwasserTyp: "MR",
            Temperatur_C: 10);
        var result = HydraulikEngine.Berechne(input);
        Assert.NotNull(result);

        var mr = HydraulikEngine.Ablagerungsgefahr("MR", result!.A_T, result.V_T, result.Kb, result.Rhy_T, result.Ny);
        var s = HydraulikEngine.Ablagerungsgefahr("S", result.A_T, result.V_T, result.Kb, result.Rhy_T, result.Ny);

        Assert.True(mr.Vc >= 0 && s.Vc >= 0);
        Assert.True(mr.Ic >= 0 && s.Ic >= 0);
        Assert.True(mr.TauC >= 0 && s.TauC >= 0);
        Assert.True(Math.Abs(mr.Vc - s.Vc) > 1e-6);
    }

    [Fact]
    public void KinematischeViskositaet_SinktImUeblichenTemperaturbereich()
    {
        var nu0 = HydraulikEngine.KinematischeViskositaet(0);
        var nu20 = HydraulikEngine.KinematischeViskositaet(20);
        var nu40 = HydraulikEngine.KinematischeViskositaet(40);

        Assert.True(nu0 > nu20);
        Assert.True(nu20 > nu40);
    }

    [Fact]
    public void HydraulikPanelViewModel_BegrenztTemperaturAufGueltigenBereich()
    {
        var vm = new HydraulikPanelViewModel();

        vm.Temperatur = -5;
        Assert.Equal(0, vm.Temperatur);

        vm.Temperatur = 60;
        Assert.Equal(40, vm.Temperatur);
    }
}
