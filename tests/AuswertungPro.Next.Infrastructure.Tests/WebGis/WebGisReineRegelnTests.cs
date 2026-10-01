using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

public sealed class WebGisReineRegelnTests
{
    [Theory]
    [InlineData("0", 100)]
    [InlineData("2", 102)]
    [InlineData("4", 104)]
    public void Zustandcode_bildet_klasse_ab(string kl, int code)
        => Assert.Equal(code, WebGisFeldkarte.ZustandCode(kl));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("x")]
    [InlineData("5")]
    public void Zustandcode_unbekannt_ist_null(string kl)
        => Assert.Null(WebGisFeldkarte.ZustandCode(kl));

    [Fact]
    public void Bemerkung_leer_neu_gibt_null()
        => Assert.Null(WebGisBemerkung.Zusammenfuehren("Tiefe 1.80m", ""));

    [Fact]
    public void Bemerkung_alt_leer_nimmt_neu()
        => Assert.Equal("Saniert 2026", WebGisBemerkung.Zusammenfuehren("", "Saniert 2026"));

    [Fact]
    public void Bemerkung_bereits_enthalten_gibt_null()
        => Assert.Null(WebGisBemerkung.Zusammenfuehren("Saniert 2026 Tiefe 2.17m", "Saniert 2026"));

    [Fact]
    public void Bemerkung_fuehrt_beide_zusammen()
        => Assert.Equal("Tiefe 1.80m · Saniert 2026",
            WebGisBemerkung.Zusammenfuehren("Tiefe 1.80m", "Saniert 2026"));

    [Fact]
    public void Saniert_nur_bei_ausgefuehrter_akte()
    {
        var recId = Guid.NewGuid();
        var akte = new ObjektAkte { Art = "sanierung", Bezuege = { recId } };
        akte.Werte["sanierung.s_status"] = new ObjektFeldWert { Text = "Ausgeführt" };
        var akten = new List<ObjektAkte> { akte };

        Assert.True(WebGisSaniertKriterium.IstSaniert(akten, recId));
        Assert.False(WebGisSaniertKriterium.IstSaniert(akten, Guid.NewGuid()));
    }

    [Fact]
    public void Saniert_ignoriert_nicht_ausgefuehrte_akte()
    {
        var recId = Guid.NewGuid();
        var akte = new ObjektAkte { Art = "sanierung", Bezuege = { recId } };
        akte.Werte["sanierung.s_status"] = new ObjektFeldWert { Text = "Geplant" };
        Assert.False(WebGisSaniertKriterium.IstSaniert(new List<ObjektAkte> { akte }, recId));
    }
}
