using AuswertungPro.Next.Application.Xtf.Dss;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using static AuswertungPro.Next.Infrastructure.Tests.XtfDssExportTests;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class DssMaterialZuordnungTests
{
    [Fact]
    public void Jede_angebotene_Materialauswahl_hat_ein_Normziel_oder_eine_ausdrueckliche_offene_Entscheidung()
    {
        var alle = DssMaterialZuordnung.Alle();
        Assert.Equal(68, alle.Count);
        Assert.All(alle, e =>
        {
            Assert.True(e.Erfasst, e.Auswahl);
            if (e.Normwert is null) Assert.Contains("Fachliche Zuordnung offen", e.Hinweis);
            else Assert.Equal(e.Normwert, DssExportSchema.Normalisiere(e.FeldId == "haltung.material" ? "Haltung" : "Normschacht", "Material", e.Normwert));
        });
    }

    [Theory]
    [InlineData("Asbestzement (AZ)", "Asbestzement")]
    [InlineData("Faserzement (FZ)", "Faserzement")]
    [InlineData("Ortsbeton (OB)", "Beton_Ortsbeton")]
    [InlineData("Hartpolyethylen (HPE)", "Kunststoff_Hartpolyethylen")]
    [InlineData("Guss, duktil (GD)", "Guss_duktil")]
    public void Eindeutige_Detailauswahl_erreicht_den_Export(string text, string norm)
    {
        var p = Projekt();
        p.Data[0].SetFieldValue(FieldKeys.PipeMaterial, text, FieldSource.Manual, true);
        WithExport(p, doc => Assert.Equal(norm, Wert(doc, "Haltung", "Material")));
    }

    [Theory]
    [InlineData("GFK (GFK)")]
    [InlineData("Guss, unbekannt (GU)")]
    [InlineData("Spezialzement, armiert (SBR)")]
    public void Offene_Detailauswahl_ersetzt_kein_bisheriges_Material_stillschweigend(string text)
    {
        var p = Projekt();
        p.Objektakten[0].Quellen.Single(q => q.Klasse == "Haltung").Werte["Material"] = "Beton_Normalbeton";
        p.Data[0].SetFieldValue(FieldKeys.PipeMaterial, text, FieldSource.Manual, true);
        var r = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true));
        Assert.False(r.Ok); Assert.Contains(text, r.Fehler); Assert.Contains("Fachliche Zuordnung offen", r.Fehler);
    }
}
