using System.Threading.Tasks;
using AuswertungPro.Next.Application.WebGis;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Die Nachpruefung direkt vor dem Anlegen einer Massnahme verwendet dieselbe Regel wie der Plan: Eine
/// Reparatur 2020 im WebGIS haelt eine Reparatur 2026 nicht auf — eine inzwischen angelegte 2026 aber schon.
/// </summary>
public sealed partial class WebGisExportUseCaseTests
{
    [Theory]
    [InlineData("01.01.2020", true)]
    [InlineData("01.01.2026", false)]
    [InlineData(null, false)]
    public async Task Vor_dem_anlegen_zaehlt_das_jahr_der_vorhandenen_massnahme(string? beginn, bool angelegt)
    {
        var (plan, _, san) = PlanMitMassnahme(mitFeldaenderung: false);
        san.Anzeige.Add("Art: Reparatur");
        san.Anzeige.Add("Status: Ausgeführt");
        san.Anzeige.Add("Verfahren: Vermörtelung");
        san.Felder[WebGisSanierungFeldkarte.SanierungsjahrRef] = "2026-01-01T00:00:00.000Z";
        var client = new FakeClient
        {
            Lese = (_, _) =>
            {
                var s = HaltungMitZustand("102");
                s.Sanierungen.Add(new WebGisSanierungZeile
                {
                    Beginn = beginn, Art = "Reparatur", Status = "Ausgeführt", Verfahren = "Vermörtelung", GlobalId = "alt",
                });
                return s;
            },
        };

        await new WebGisExportUseCase(client).FuehreAusAsync(plan, probelauf: false);

        Assert.Equal(angelegt, san.Geschrieben);
        Assert.Equal(angelegt ? 1 : 0, client.MassnahmenAngelegt);
        if (!angelegt) Assert.Contains("inzwischen", san.SchreibFehler);
    }
}
