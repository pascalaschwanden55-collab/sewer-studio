using System;
using System.IO;
using AuswertungPro.Next.Application.Kostenanalyse;
using AuswertungPro.Next.Infrastructure.Kostenanalyse;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Kostenanalyse;

public sealed class KostenanalyseBerichtSchreiberTests : IDisposable
{
    private readonly string _wurzel = Directory.CreateTempSubdirectory().FullName;

    public void Dispose()
    {
        try { Directory.Delete(_wurzel, recursive: true); } catch { }
    }

    private static KostenanalyseMessErgebnis Ergebnis() => new()
    {
        Gesamt = 55,
        MitVorschlag = 41,
        Enthalten = 14,
        PositionenRichtig = 96,
        PositionenZuviel = 12,
        PositionenFehlend = 23
    };

    [Fact]
    public void Schreibt_Bericht_und_Pruefsumme()
    {
        var pfad = KostenanalyseBerichtSchreiber.Schreibe(
            _wurzel, Ergebnis(), new DateTime(2026, 8, 20, 14, 30, 0, DateTimeKind.Utc));

        Assert.True(File.Exists(pfad));
        Assert.True(File.Exists(pfad + ".sha256"));
        Assert.Equal(64, File.ReadAllText(pfad + ".sha256").Trim().Length);
    }

    [Fact]
    public void Der_Bericht_enthaelt_Abdeckung_und_Treffer()
    {
        var pfad = KostenanalyseBerichtSchreiber.Schreibe(
            _wurzel, Ergebnis(), new DateTime(2026, 8, 20, 14, 30, 0, DateTimeKind.Utc));

        var inhalt = File.ReadAllText(pfad);

        Assert.Contains("\"gesamt\": 55", inhalt);
        Assert.Contains("\"mitVorschlag\": 41", inhalt);
        Assert.Contains("abdeckung", inhalt);
        Assert.Contains("positionenFehlend", inhalt);
    }

    [Fact]
    public void Ein_bestehender_Bericht_wird_nie_ueberschrieben()
    {
        var zeitpunkt = new DateTime(2026, 8, 20, 14, 30, 0, DateTimeKind.Utc);
        KostenanalyseBerichtSchreiber.Schreibe(_wurzel, Ergebnis(), zeitpunkt);

        Assert.Throws<IOException>(
            () => KostenanalyseBerichtSchreiber.Schreibe(_wurzel, Ergebnis(), zeitpunkt));
    }

    [Fact]
    public void Der_Bericht_sagt_ausdruecklich_dass_er_keine_Freigabe_ist()
    {
        var pfad = KostenanalyseBerichtSchreiber.Schreibe(
            _wurzel, Ergebnis(), new DateTime(2026, 8, 20, 15, 0, 0, DateTimeKind.Utc));

        Assert.Contains("keine Freigabe", File.ReadAllText(pfad));
    }
}
