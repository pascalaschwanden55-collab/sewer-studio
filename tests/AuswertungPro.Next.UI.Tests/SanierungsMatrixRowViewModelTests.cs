using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SanierungsMatrixRowViewModelTests
{
    [Fact]
    public void InitFrom_setzt_gespeicherten_Zustand_ohne_Neuberechnung()
    {
        var changes = 0;
        var option = Option(manualQuantity: false);
        var row = Row("42.5", _ => changes++);

        row.InitFrom(option, 120m, 42.5m, true, false, true, false, true);

        Assert.Equal(0, changes);
        Assert.Same(option, row.SelectedMeasure);
        Assert.Equal(42.5m, row.Menge);
        Assert.Equal(120m, row.Total);
        Assert.True(row.OptVerkehrsdienst);
        Assert.True(row.OptFraesen);
        Assert.True(row.OptDokumentation);
    }

    [Fact]
    public void Auswahl_einer_Laengenmassnahme_liest_Punktwert_kulturunabhaengig()
    {
        var changes = 0;
        var row = Row("42.50", _ => changes++);

        row.SelectedMeasure = Option(manualQuantity: false);

        Assert.Equal(42.50m, row.Menge);
        Assert.True(row.IsMengeReadOnly);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Auswahl_einer_Stueckmassnahme_startet_mit_eins_und_bleibt_editierbar()
    {
        var changes = 0;
        var row = Row("42.5", _ => changes++);

        row.SelectedMeasure = Option(manualQuantity: true);

        Assert.Equal(1m, row.Menge);
        Assert.False(row.IsMengeReadOnly);
        Assert.Equal(1, changes);
    }

    private static SanierungMatrixRowVm Row(
        string length,
        Action<SanierungMatrixRowVm> onChanged)
        => new(new HaltungRecord(), "H-01", "300", length, 0, onChanged);

    private static MeasureOption Option(bool manualQuantity)
        => new("M1", "Massnahme", "Reparatur", manualQuantity, "HAUPT");
}
