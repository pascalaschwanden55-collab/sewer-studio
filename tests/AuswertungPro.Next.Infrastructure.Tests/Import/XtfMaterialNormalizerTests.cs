using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Sichert, dass der SIA405-Materialwert im Programm auch ankommt.
///
/// Hintergrund: Rohrmaterial ist ein Auswahlfeld. Was nicht in FieldCatalog.ComboItems steht,
/// zeigt die Auswahlbox als LEER an — der Wert steckt zwar in den Daten, ist aber unsichtbar.
/// Der Normalisierer lieferte Werte wie "Kunststoff PVC", die es in der Liste nie gab; ueber alle
/// Projekte betraf das 99 von 330 Haltungen. Die Rohwerte unten stammen aus echten IKAS-Exporten.
/// </summary>
public sealed class XtfMaterialNormalizerTests
{
    [Theory]
    // Kataster-Schreibweise mit Praefix. "Polyvinilchlorid" ist im Kataster mit i geschrieben —
    // die alte Regex suchte nach y und griff darum nie (82x in echten Dateien).
    [InlineData("Kunststoff_Polyvinilchlorid", "Polyvinylchlorid")]
    [InlineData("Kunststoff_Polyvinylchlorid", "Polyvinylchlorid")]
    [InlineData("Polyvinylchlorid", "Polyvinylchlorid")]
    [InlineData("Kunststoff_Hartpolyethylen", "Hartpolyethylen")]
    [InlineData("Kunststoff_Polyethylen", "Polyethylen")]
    [InlineData("Polyethylen", "Polyethylen")]
    [InlineData("Kunststoff_Polypropylen", "Polypropylen")]
    [InlineData("Polypropylen", "Polypropylen")]
    [InlineData("Kunststoff_Epoxydharz", "Epoxydharz")]
    [InlineData("Beton_Normalbeton", "Beton")]
    [InlineData("Beton", "Beton")]
    [InlineData("Guss_Grauguss", "Guss")]
    [InlineData("Steinzeug", "Steinzeug")]
    [InlineData("Zement", "Zement")]
    [InlineData("Faserzement", "Faserzement")]
    [InlineData("Ton", "Ton")]
    public void Every_real_catalog_value_becomes_a_selectable_entry(string rohwert, string erwartet)
    {
        var normalisiert = XtfValueNormalizer.NormalizeSiaMaterial(rohwert);

        Assert.Equal(erwartet, normalisiert);
        // Der eigentliche Punkt: Der Wert muss in der Auswahlliste stehen, sonst bleibt das Feld leer.
        Assert.Contains(normalisiert, FieldCatalog.GetComboItems(FieldKeys.PipeMaterial));
    }

    [Fact]
    public void Hartpolyethylen_wins_over_the_shorter_polyethylen_rule()
    {
        // Reihenfolge-Falle: "Kunststoff_Hartpolyethylen" enthaelt "polyethylen".
        Assert.Equal("Hartpolyethylen", XtfValueNormalizer.NormalizeSiaMaterial("Kunststoff_Hartpolyethylen"));
    }

    [Fact]
    public void Empty_stays_empty()
    {
        Assert.Equal("", XtfValueNormalizer.NormalizeSiaMaterial(""));
        Assert.Equal("", XtfValueNormalizer.NormalizeSiaMaterial("   "));
    }

    [Fact]
    public void An_unknown_material_is_passed_through_readably()
    {
        // Unbekanntes lieber lesbar durchreichen als verwerfen: Der Wert ist dann zwar nicht
        // waehlbar, aber die Information geht nicht verloren.
        Assert.Equal("Irgendwas Neues", XtfValueNormalizer.NormalizeSiaMaterial("Irgendwas_Neues"));
    }

    [Fact]
    public void Asbestzement_wird_nicht_zu_Zement()
    {
        // Gemessen am AWU-Kantonsexport: 247 Haltungen tragen Asbestzement.
        // Die Zement-Regel schluckte sie, weil "Asbestzement" das Wort enthaelt.
        // Asbestzement ist ein eigener Werkstoff - andere Sanierung, anderer
        // Arbeitsschutz. Ein falsches Material ist schlimmer als ein grobes.
        Assert.Equal("Asbestzement", XtfValueNormalizer.NormalizeSiaMaterial("Asbestzement"));
    }

    [Fact]
    public void Faserzement_und_Zement_bleiben_unveraendert()
    {
        // Gegenprobe: die neue Regel darf die zwei bestehenden nicht verdraengen.
        Assert.Equal("Faserzement", XtfValueNormalizer.NormalizeSiaMaterial("Faserzement"));
        Assert.Equal("Zement", XtfValueNormalizer.NormalizeSiaMaterial("Zement"));
    }
}
