using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.Domain.Models.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers.Lookup;

public sealed class DossierBatchCreationUseCaseTests
{
    private static DossierProposal Vorschlag()
    {
        var parzelle = new ParcelInfo("439", 1206, "Musterdorf", 1139, "CH1",
            "POLYGON((0 0,1 0,1 1,0 0))", "https://example.invalid/gb");

        var eintrag = new LandRegistryEntry("Musterstrasse", "30", "6472", "Musterdorf", new[]
        {
            new LandRegistryOwner("Lit.A", "Kurt Beispiel", "Musterstrasse 30, 6472 Musterdorf", "1/2 Miteigentum"),
            new LandRegistryOwner("Lit.B", "Rita Beispiel", "Musterstrasse 30, 6472 Musterdorf", "1/2 Miteigentum")
        }, NoOwnerRegistered: false);

        var leitungen = new[]
        {
            new ProposedHolding("36051-36329", true, true, true, "Lage"),
            new ProposedHolding("36329-35558", false, true, false, "Lage")
        };

        return new DossierProposal(parzelle, eintrag, leitungen,
            "Liegenschaft Nr. 439 Beispiel", Selectable: true, SkipReason: "");
    }

    [Fact]
    public void Erzeugt_ein_Dossier_mit_beiden_Eigentuemerzeilen()
    {
        var ids = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase)
        {
            ["36051-36329"] = Guid.NewGuid()
        };

        var dossiers = DossierBatchCreationUseCase.Build(
            new[] { new DossierCreationSelection(Vorschlag(), new[] { "36051-36329" }) },
            ids,
            Array.Empty<string>());

        var dossier = Assert.Single(dossiers);
        Assert.Equal("Liegenschaft Nr. 439 Beispiel", dossier.Name);
        Assert.Equal("439", dossier.ParcelNumbers);
        Assert.Equal("Musterdorf", dossier.Municipality);
        Assert.Equal(1206, dossier.MunicipalityBfsNr);
        Assert.Equal("Musterstrasse", dossier.Address);
        Assert.Equal("30", dossier.HouseNumbers);
        Assert.Equal("6472", dossier.PostalCode);
        Assert.Equal("Musterdorf", dossier.Town);

        Assert.Equal(2, dossier.Owners.Count);
        Assert.Equal("Kurt Beispiel", dossier.Owners[0].Name);
        Assert.Equal("Rita Beispiel", dossier.Owners[1].Name);
        Assert.All(dossier.Owners, o => Assert.Equal("", o.Phone));

        Assert.Equal(new[] { ids["36051-36329"] }, dossier.HoldingIds);
    }

    [Fact]
    public void Nur_angehakte_Leitungen_kommen_hinein()
    {
        var ids = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase)
        {
            ["36051-36329"] = Guid.NewGuid(),
            ["36329-35558"] = Guid.NewGuid()
        };

        var dossiers = DossierBatchCreationUseCase.Build(
            new[] { new DossierCreationSelection(Vorschlag(), Array.Empty<string>()) },
            ids,
            Array.Empty<string>());

        Assert.Empty(Assert.Single(dossiers).HoldingIds);
    }

    [Fact]
    public void Eine_Leitung_ohne_bekannte_Kennung_wird_uebersprungen()
    {
        var dossiers = DossierBatchCreationUseCase.Build(
            new[] { new DossierCreationSelection(Vorschlag(), new[] { "36051-36329" }) },
            new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<string>());

        Assert.Empty(Assert.Single(dossiers).HoldingIds);
    }

    [Fact]
    public void Ein_nicht_waehlbarer_Vorschlag_erzeugt_kein_Dossier()
    {
        var gesperrt = Vorschlag() with { Selectable = false, SkipReason = "kein Eigentümer" };

        var dossiers = DossierBatchCreationUseCase.Build(
            new[] { new DossierCreationSelection(gesperrt, new[] { "36051-36329" }) },
            new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<string>());

        Assert.Empty(dossiers);
    }

    [Fact]
    public void Ein_Stapel_Dossier_bekommt_die_Schaechte_seiner_Parzelle()
    {
        // Vorher vergab der Stapel ueberhaupt keine Schaechte: die Dossiers
        // kamen leer heraus, und niemand sah einer leeren Liste an, ob es
        // keine gibt oder ob nur niemand gesucht hat.
        var ids = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase)
        {
            ["439.01-36051"] = Guid.NewGuid()
        };

        var dossiers = DossierBatchCreationUseCase.Build(
            new[] { new DossierCreationSelection(Vorschlag(), new[] { "439.01-36051" }) },
            ids,
            new[] { "439.01", "36051", "439.02", "512.01" });

        var dossier = Assert.Single(dossiers);

        // Knoten der Leitung zuerst, danach der nur ueber seinen Namen
        // gefundene Schacht derselben Parzelle. "512.01" gehoert nicht dazu.
        Assert.Equal(new[] { "439.01", "36051", "439.02" }, dossier.ShaftNumbers);
    }
}
