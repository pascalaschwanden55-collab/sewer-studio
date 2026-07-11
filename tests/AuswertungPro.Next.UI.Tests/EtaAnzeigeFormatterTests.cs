using System;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Anzeige der ETA in der Statuszeile ("12.4 Frames/s · Rest ~ 04:12").</summary>
public sealed class EtaAnzeigeFormatterTests
{
    [Fact]
    public void Rate_und_restzeit_werden_kompakt_formatiert()
    {
        var text = EtaAnzeigeFormatter.Format(new EtaErgebnis(TimeSpan.FromSeconds(252), 12.37));
        Assert.Equal("12.4 Frames/s · Rest ~ 04:12", text);
    }

    [Fact]
    public void Stunden_erscheinen_erst_ab_einer_stunde()
    {
        var text = EtaAnzeigeFormatter.Format(new EtaErgebnis(TimeSpan.FromMinutes(75), 2.0));
        Assert.Equal("2.0 Frames/s · Rest ~ 1:15:00", text);
    }

    [Fact]
    public void Ohne_schaetzung_bleibt_die_zeile_leer()
    {
        Assert.Equal(string.Empty, EtaAnzeigeFormatter.Format(new EtaErgebnis(null, null)));
        Assert.Equal(string.Empty, EtaAnzeigeFormatter.Format(null));
    }

    [Fact]
    public void Nur_rate_ohne_restzeit_zeigt_die_rate()
    {
        var text = EtaAnzeigeFormatter.Format(new EtaErgebnis(null, 5.55));
        Assert.Equal("5.6 Frames/s", text);
    }
}
