using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

public sealed class WebGisBegriffeObjektakteTests
{
    [Theory]
    [InlineData("haltung.usage", "Regenabwasser")]
    [InlineData("haltung.profile", "Kreisprofil (K)")]
    [InlineData("schacht.funktion", "Pumpenschacht")]
    [InlineData("haltung.status", "Tot/Aufgehoben, verfüllt")]
    public void Objektakte_schreibt_die_webgis_beschriftung_ins_speicherfeld(string feldId, string text)
        => Assert.Equal(text, ObjektaktenBearbeitung.Normalisiere(FieldCatalog.Objektfelder.Feld(feldId), text));

    [Fact]
    public void Zustandsklasse_bleibt_ziffer()
        => Assert.Equal("2", ObjektaktenBearbeitung.Normalisiere(FieldCatalog.Objektfelder.Feld("haltung.condition"), "Mittlere Mängel (Z2)"));
}
