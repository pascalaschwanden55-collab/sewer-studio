using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers.Lookup;

public sealed class DossierLookupCompositionTests
{
    [Fact]
    public void Die_Leser_erfuellen_ihre_Vertraege_und_passen_in_den_Anwendungsfall()
    {
        // Kein Netzzugriff: die Konstruktoren rufen nichts ab. Geprueft wird
        // allein, dass die Teile zusammenpassen — genau das, was beim
        // Zusammenbau schiefgehen kann.
        using var gateway = new GeoUrHttpGateway();

        IParcelLookup parzellen = new UriParcelWfsClient(gateway);
        ILandRegistryLookup grundbuch = new UriLandRegistryClient(gateway);
        ISewerNetworkLookup netz = new UriSewerNetworkWfsClient(gateway);

        var anwendungsfall = new DossierBatchProposalUseCase(parzellen, grundbuch, netz);

        Assert.NotNull(anwendungsfall);
    }

    [Fact]
    public void Ohne_Leser_gibt_es_keinen_Anwendungsfall()
    {
        using var gateway = new GeoUrHttpGateway();

        Assert.Throws<System.ArgumentNullException>(() => new DossierBatchProposalUseCase(
            null!, new UriLandRegistryClient(gateway), new UriSewerNetworkWfsClient(gateway)));
    }
}
