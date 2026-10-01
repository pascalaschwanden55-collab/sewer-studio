using AuswertungPro.Next.Application.WebGis;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Das Verfahren haengt von der Art ab, und dieselbe Nummer bedeutet je Art etwas anderes.
/// Fehlt die gefilterte Liste, darf NICHT in der ungefilterten geraten werden (Pruefung 22.09.2026).
/// </summary>
public sealed class WebGisKatalogFallbackTests
{
    [Fact]
    public void Ohne_gefilterte_liste_wird_nicht_aus_der_ungefilterten_geraten()
    {
        var k = new WebGisSanierungKatalog();
        k.Setze(WebGisSanierungFeldkarte.VerfahrenRef, new[] { ("27", "Schlauchverfahren"), ("33", "Vermörtelung") });

        Assert.Null(k.Schluessel(WebGisSanierungFeldkarte.VerfahrenRef, "Vermörtelung", "2"));
        Assert.Equal("33", k.Schluessel(WebGisSanierungFeldkarte.VerfahrenRef, "Vermörtelung")); // ohne Filter bleibt die Liste nutzbar
    }
}
