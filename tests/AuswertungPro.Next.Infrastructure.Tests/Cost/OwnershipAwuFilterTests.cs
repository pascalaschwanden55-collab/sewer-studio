using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Infrastructure.Costs;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Cost;

/// <summary>
/// AWU-Eigentumsfilter fuer das NPK-135-LV: nur Haltungen/Schaechte im Eigentum von
/// Abwasser Uri (AWU); Private werden separat abgehandelt. Erkennung ist tolerant
/// (Whitelist-Wert "AWU" ODER Import-Freitext "Abwasser Uri").
/// </summary>
public sealed class OwnershipAwuFilterTests
{
    [Theory]
    [InlineData("AWU")]
    [InlineData("awu")]
    [InlineData(" AWU ")]
    [InlineData("Abwasser Uri")]
    [InlineData("abwasser uri")]
    [InlineData("Abwasser Uri (AWU)")]
    public void IsAwu_erkennt_awu_und_freitext(string owner)
    {
        Assert.True(OwnershipAwuFilter.IsAwu(owner));
    }

    [Theory]
    [InlineData("Privat")]
    [InlineData("Gemeinde")]
    [InlineData("Kanton")]
    [InlineData("Bund")]
    [InlineData("Gemeinde Buerglen")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsAwu_lehnt_nicht_awu_ab(string? owner)
    {
        Assert.False(OwnershipAwuFilter.IsAwu(owner));
    }

    [Fact]
    public void AwuSchachtKeys_nimmt_nur_awu_schaechte_normalisiert()
    {
        var paare = new (string?, string?)[]
        {
            ("KS 60191", "AWU"),           // AWU -> rein, normalisiert "KS60191"
            ("80551", "Abwasser Uri"),     // Freitext AWU -> rein
            ("80631", "Privat"),           // Privat -> raus
            ("80700", "Gemeinde"),         // Gemeinde -> raus
            ("  ", "AWU"),                 // leere Nummer -> raus
            ("KS999", null),               // kein Eigentuemer -> raus
        };

        var keys = OwnershipAwuFilter.AwuSchachtKeys(paare);

        Assert.Equal(2, keys.Count);
        Assert.Contains("KS60191", keys);   // Innen-Whitespace entfernt, Grossbuchstaben
        Assert.Contains("80551", keys);
        Assert.DoesNotContain("80631", keys);
        Assert.DoesNotContain("80700", keys);
    }

    [Fact]
    public void AwuSchachtKeys_matcht_kostendatei_nummer_ueber_normalisierung()
    {
        // Schacht-Datensatz "KS 60191" (mit Leerzeichen) muss die Kostendatei-Nummer
        // "ks60191" (ohne Leerzeichen, klein) treffen.
        var keys = OwnershipAwuFilter.AwuSchachtKeys(new (string?, string?)[] { ("KS 60191", "AWU") });

        Assert.Contains(OwnershipAwuFilter.NormalizeSchacht("ks60191"), keys);
    }

    [Fact]
    public void AwuSchachtKeys_leere_eingabe_ist_leer()
    {
        Assert.Empty(OwnershipAwuFilter.AwuSchachtKeys(Enumerable.Empty<(string?, string?)>()));
    }
}
