using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerStreckenschadenWorkflowTests
{
    [Fact]
    public void ApplyChecked_setzt_default_anfang_wenn_bisher_kein_typ_vorhanden_ist()
    {
        var result = VsaCodeExplorerStreckenschadenWorkflow.ApplyChecked(" ");

        Assert.True(result.IsStreckenschaden);
        Assert.Equal("Anfang", result.StreckenschadenTyp);
        Assert.True(result.Presentation.ShowTypPanel);
        Assert.Equal(0, result.Presentation.SelectedTypIndex);
    }

    [Fact]
    public void ApplyChecked_behaelt_ende_und_markiert_ende_index()
    {
        var result = VsaCodeExplorerStreckenschadenWorkflow.ApplyChecked("Ende");

        Assert.True(result.IsStreckenschaden);
        Assert.Equal("Ende", result.StreckenschadenTyp);
        Assert.True(result.Presentation.ShowTypPanel);
        Assert.Equal(1, result.Presentation.SelectedTypIndex);
    }

    [Fact]
    public void ApplyUnchecked_leert_typ_und_blendet_typ_panel_aus()
    {
        var result = VsaCodeExplorerStreckenschadenWorkflow.ApplyUnchecked();

        Assert.False(result.IsStreckenschaden);
        Assert.Equal("", result.StreckenschadenTyp);
        Assert.False(result.Presentation.ShowTypPanel);
        Assert.Null(result.Presentation.SelectedTypIndex);
    }

    [Fact]
    public void BuildInitial_initialisiert_aktive_strecke_mit_passendem_index()
    {
        var result = VsaCodeExplorerStreckenschadenWorkflow.BuildInitial(
            isStreckenschaden: true,
            currentTyp: "Ende");

        Assert.True(result.IsStreckenschaden);
        Assert.Equal("Ende", result.StreckenschadenTyp);
        Assert.True(result.Presentation.ShowTypPanel);
        Assert.Equal(1, result.Presentation.SelectedTypIndex);
    }

    [Fact]
    public void ApplySelectionChanged_gibt_item_text_oder_leerstring_zurueck()
    {
        Assert.Equal("Anfang", VsaCodeExplorerStreckenschadenWorkflow.ApplySelectionChanged("Anfang"));
        Assert.Equal("", VsaCodeExplorerStreckenschadenWorkflow.ApplySelectionChanged(null));
    }
}
