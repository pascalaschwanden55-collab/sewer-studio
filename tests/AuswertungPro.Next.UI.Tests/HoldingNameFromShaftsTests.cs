using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Der Haltungsname folgt den Schachtnummern — aber nur, wenn er wirklich aus
/// ihnen besteht. Die vorhandene Reihenfolge bleibt dabei erhalten.
/// </summary>
public sealed class HoldingNameFromShaftsTests
{
    [Fact]
    public void Name_in_der_Reihenfolge_unten_oben_behaelt_seine_Reihenfolge()
        => Assert.Equal(
            "77565-77500",
            HoldingNameFromShafts.Ableiten(
                aktuellerName: "77565-77564",
                altOben: "77564",
                altUnten: "77565",
                neuOben: "77500",
                neuUnten: "77565"));

    [Fact]
    public void Name_in_der_Reihenfolge_oben_unten_behaelt_seine_Reihenfolge()
        => Assert.Equal(
            "77500-77565",
            HoldingNameFromShafts.Ableiten(
                aktuellerName: "77564-77565",
                altOben: "77564",
                altUnten: "77565",
                neuOben: "77500",
                neuUnten: "77565"));

    [Fact]
    public void Auch_der_untere_Schacht_zieht_den_Namen_nach()
        => Assert.Equal(
            "77900-77564",
            HoldingNameFromShafts.Ableiten(
                aktuellerName: "77565-77564",
                altOben: "77564",
                altUnten: "77565",
                neuOben: "77564",
                neuUnten: "77900"));

    [Fact]
    public void Ein_selbst_vergebener_Name_bleibt_unangetastet()
        => Assert.Null(HoldingNameFromShafts.Ableiten(
            aktuellerName: "Jagdmatt West",
            altOben: "77564",
            altUnten: "77565",
            neuOben: "77500",
            neuUnten: "77565"));

    [Fact]
    public void Eine_leere_neue_Schachtnummer_aendert_den_Namen_nicht()
        => Assert.Null(HoldingNameFromShafts.Ableiten(
            aktuellerName: "77565-77564",
            altOben: "77564",
            altUnten: "77565",
            neuOben: "",
            neuUnten: "77565"));

    [Fact]
    public void Ohne_echte_Aenderung_entsteht_kein_neuer_Name()
        => Assert.Null(HoldingNameFromShafts.Ableiten(
            aktuellerName: "77565-77564",
            altOben: "77564",
            altUnten: "77565",
            neuOben: "77564",
            neuUnten: "77565"));

    [Fact]
    public void Zwei_gleiche_Schachtnummern_ergeben_trotzdem_den_richtigen_Namen()
        => Assert.Equal(
            "77500-77564",
            HoldingNameFromShafts.Ableiten(
                aktuellerName: "77564-77564",
                altOben: "77564",
                altUnten: "77564",
                neuOben: "77500",
                neuUnten: "77564"));

    [Fact]
    public void Leerzeichen_um_die_Werte_stoeren_die_Erkennung_nicht()
        => Assert.Equal(
            "77565-77500",
            HoldingNameFromShafts.Ableiten(
                aktuellerName: " 77565-77564 ",
                altOben: " 77564 ",
                altUnten: "77565",
                neuOben: "77500",
                neuUnten: "77565"));

    [Fact]
    public void Ohne_alte_Schachtnummern_wird_nichts_geraten()
        => Assert.Null(HoldingNameFromShafts.Ableiten(
            aktuellerName: "77565-77564",
            altOben: "",
            altUnten: "",
            neuOben: "77500",
            neuUnten: "77565"));

    [Fact]
    public void Ein_leerer_Haltungsname_wird_nicht_gefuellt()
        => Assert.Null(HoldingNameFromShafts.Ableiten(
            aktuellerName: "",
            altOben: "77564",
            altUnten: "77565",
            neuOben: "77500",
            neuUnten: "77565"));
}
