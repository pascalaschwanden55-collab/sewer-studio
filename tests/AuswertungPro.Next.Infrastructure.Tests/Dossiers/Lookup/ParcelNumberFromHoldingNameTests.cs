using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers.Lookup;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers.Lookup;

public sealed class ParcelNumberFromHoldingNameTests
{
    [Theory]
    // Ein Knoten der Form <Parzelle>.<lfd> nennt seine Parzelle.
    [InlineData("439.01-36051", "439")]
    [InlineData("36051-439.02", "439")]
    [InlineData("438.03-438.04", "438")]
    [InlineData("1273.01-7.34854", "1273")]
    public void Erkennt_die_Parzelle_aus_der_Knotenform(string name, string erwartet)
    {
        Assert.Equal(new[] { erwartet }, ParcelNumberFromHoldingName.Extract(name));
    }

    [Fact]
    public void Zwei_verschiedene_Parzellen_ergeben_zwei_Nummern()
    {
        var treffer = ParcelNumberFromHoldingName.Extract("952.02-982.03");

        Assert.Equal(new[] { "952", "982" }, treffer);
    }

    [Theory]
    // Reine Schachtnummern nennen keine Parzelle.
    [InlineData("36262-36275")]
    [InlineData("33850-7.25390")]
    [InlineData("")]
    [InlineData(null)]
    public void Ohne_Knotenform_gibt_es_keine_Nummer(string? name)
    {
        Assert.Empty(ParcelNumberFromHoldingName.Extract(name));
    }

    [Fact]
    public void ExtractAll_fasst_zusammen_und_entdoppelt()
    {
        var treffer = ParcelNumberFromHoldingName.ExtractAll(new[]
        {
            "439.01-36051", "439.02-36051", "952.02-952.03", "36262-36275", null
        });

        Assert.Equal(new[] { "439", "952" }, treffer.OrderBy(t => t.Length).ThenBy(t => t).ToArray());
    }
}
