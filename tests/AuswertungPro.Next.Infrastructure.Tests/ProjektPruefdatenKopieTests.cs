using AuswertungPro.Next.Application.UseCases.ProjektPruefung;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using System.Diagnostics;
using Xunit.Abstractions;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ProjektPruefdatenKopieTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public async Task Synthetische_Datensaetze_liefern_unveraenderte_Pruefpunkte_und_Messwerte(int anzahl)
    {
        var p = new Project();
        for (var i = 0; i < anzahl; i++)
        {
            var s = new SchachtRecord();
            s.Fields["Schachtnummer"] = i.ToString();
            s.Fields["Tiefe"] = "-2";
            p.SchaechteData.Add(s);
        }
        var uhr = Stopwatch.StartNew();
        var abschnitte = new List<double>();
        var kopie = await ProjektPruefdatenKopie.ErfasseAsync(p, () =>
        {
            abschnitte.Add(uhr.Elapsed.TotalMilliseconds);
            uhr.Restart();
            return Task.CompletedTask;
        }, default);
        abschnitte.Add(uhr.Elapsed.TotalMilliseconds);
        var kopieMs = abschnitte.Sum();
        uhr.Restart();
        Assert.True(ProjektPruefdatenKopie.Gleich(kopie, p));
        var vergleichMs = uhr.Elapsed.TotalMilliseconds;
        var live = ProjektPruefregeln.Pruefe(p, _ => null);
        var abbild = ProjektPruefregeln.Pruefe(kopie, _ => null);
        Assert.Equal(live.Haltungen, abbild.Haltungen);
        Assert.Equal(live.Schaechte, abbild.Schaechte);
        Assert.Equal(live.Punkte.ToArray(), abbild.Punkte.ToArray());
        output.WriteLine($"{anzahl} Datensätze: Kopie {kopieMs:F1} ms, längster UI-Abschnitt {abschnitte.Max():F1} ms, Vergleich {vergleichMs:F1} ms, Hinweise {live.Punkte.Count}");
    }

    [Fact]
    public async Task Kopie_liefert_dieselben_fuenf_Bereiche_und_bleibt_vom_Original_getrennt()
    {
        var p = new Project();
        var h = new HaltungRecord { Protocol = new() { Current = new() } };
        h.SetFieldValue(FieldKeys.HoldingName, "1-2", FieldSource.Manual, true);
        h.SetFieldValue(FieldKeys.HoldingLengthMeters, "10", FieldSource.Manual, true);
        h.SetFieldValue(FieldKeys.Link, "fehlt.mp4", FieldSource.Manual, true);
        h.Protocol.Current.Entries.Add(new ProtocolEntry { Code = "BAB", MeterStart = 12, Ai = new() });
        p.Data.Add(h);
        var s = new SchachtRecord();
        s.SetFieldValue("Tiefe", "-2", FieldSource.Manual, true);
        p.SchaechteData.Add(s);
        p.Objektakten.Add(new ObjektAkte { Art = "bauwerksteil", Bezuege = [h.Id] });

        var kopie = await ProjektPruefdatenKopie.ErfasseAsync(p, () => Task.CompletedTask, default);
        var live = ProjektPruefregeln.Pruefe(p, _ => "Datei fehlt.");
        var abbild = ProjektPruefregeln.Pruefe(kopie, _ => "Datei fehlt.");

        Assert.Equal(live.Haltungen, abbild.Haltungen);
        Assert.Equal(live.Schaechte, abbild.Schaechte);
        Assert.Equal(live.Punkte.ToArray(), abbild.Punkte.ToArray());
        Assert.True(ProjektPruefdatenKopie.Gleich(kopie, p));
        h.Protocol.Current.Entries[0].Ai!.Accepted = true;
        Assert.False(ProjektPruefdatenKopie.Gleich(kopie, p));
    }

    /// <summary>Deepscan 02.10.2026, R3: Die Kopie traegt die Fotopfade, sonst sieht die UI keine Temp-Fotos.</summary>
    [Fact]
    public async Task Kopie_traegt_Fotopfade_fuer_die_Temp_Pruefung()
    {
        var p = new Project();
        var h = new HaltungRecord { Protocol = new() { Current = new() } };
        var e = new ProtocolEntry { Code = "BAB" };
        e.FotoPaths.Add(@"C:\Sim\Temp\vsa_foto1.png");
        h.Protocol.Current.Entries.Add(e);
        p.Data.Add(h);

        var kopie = await ProjektPruefdatenKopie.ErfasseAsync(p, () => Task.CompletedTask, default);
        string[] temp = [@"C:\Sim\Temp"];
        var live = ProjektPruefregeln.Pruefe(p, _ => null, default, temp);
        var abbild = ProjektPruefregeln.Pruefe(kopie, _ => null, default, temp);

        Assert.Contains(abbild.Punkte, x => x.Bereich == ProjektPruefbereich.Dateien && x.EintragId == e.EntryId);
        Assert.Equal(live.Punkte.ToArray(), abbild.Punkte.ToArray());
        Assert.True(ProjektPruefdatenKopie.Gleich(kopie, p));
        e.FotoPaths[0] = @"D:\Projekt\Fotos\vsa_foto1.png";
        Assert.False(ProjektPruefdatenKopie.Gleich(kopie, p));
        kopie.Data[0].Protocol!.Current.Entries[0].FotoPaths.Clear();
        Assert.Equal(@"D:\Projekt\Fotos\vsa_foto1.png", Assert.Single(e.FotoPaths));
    }

    [Fact]
    public async Task Viele_Befunde_einer_Haltung_werden_in_kleinen_Abschnitten_erfasst()
    {
        var p = new Project();
        var h = new HaltungRecord { Protocol = new() { Current = new() } };
        p.Data.Add(h);
        for (var i = 0; i < 10000; i++)
            h.Protocol.Current.Entries.Add(new ProtocolEntry { Code = "BAB", Ai = new() });
        var pausen = 0;

        var kopie = await ProjektPruefdatenKopie.ErfasseAsync(p, () =>
        { pausen++; return Task.CompletedTask; }, default);

        Assert.True(pausen >= 150);
        Assert.True(ProjektPruefdatenKopie.Gleich(kopie, p));
        Assert.Equal(10000, ProjektPruefregeln.Pruefe(kopie, _ => null).Punkte.Count(x =>
            x.Bereich == ProjektPruefbereich.KiBefunde));
    }

    [Fact]
    public async Task Objektakte_und_Bestandsfelder_werden_auch_ohne_Aenderungsmeldung_erkannt()
    {
        var p = new Project();
        var s = new SchachtRecord(); p.SchaechteData.Add(s);
        var akte = new ObjektAkte { Art = "deckel", Bezuege = [s.Id],
            Werte = new() { ["deckel.hoehe"] = new() { Text = "520" } } };
        p.Objektakten.Add(akte);
        var stand = await ProjektPruefdatenKopie.ErfasseAsync(p, () => Task.CompletedTask, default);
        akte.Werte["deckel.hoehe"].Text = "521";
        Assert.False(ProjektPruefdatenKopie.Gleich(stand, p));
        akte.Werte["deckel.hoehe"].Text = "520";
        akte.Bezuege.Clear();
        Assert.False(ProjektPruefdatenKopie.Gleich(stand, p));
    }

    [Fact]
    public async Task NaN_Meter_bleibt_als_Prueffehler_sichtbar_und_gilt_als_unveraendert()
    {
        var p = new Project();
        var h = new HaltungRecord { Protocol = new() { Current = new() } };
        h.Protocol.Current.Entries.Add(new ProtocolEntry { MeterStart = double.NaN });
        p.Data.Add(h);
        var stand = await ProjektPruefdatenKopie.ErfasseAsync(p, () => Task.CompletedTask, default);
        Assert.True(ProjektPruefdatenKopie.Gleich(stand, p));
        Assert.Equal(ProjektPruefregeln.Pruefe(p, _ => null).Punkte.ToArray(),
            ProjektPruefregeln.Pruefe(stand, _ => null).Punkte.ToArray());
        Assert.Contains(ProjektPruefregeln.Pruefe(stand, _ => null).Punkte,
            x => x.Bereich == ProjektPruefbereich.Meterangaben);
    }

    [Fact]
    public async Task Geaenderte_Reihenfolge_von_Schachtfeld_Aliasen_wird_erkannt()
    {
        var p = new Project();
        var s = new SchachtRecord();
        s.Fields["Tiefe"] = "2";
        s.Fields["TIEFE"] = "3";
        p.SchaechteData.Add(s);
        var stand = await ProjektPruefdatenKopie.ErfasseAsync(p, () => Task.CompletedTask, default);
        Assert.True(ProjektPruefdatenKopie.Gleich(stand, p));
        s.Fields = new() { ["TIEFE"] = "3", ["Tiefe"] = "2" };
        Assert.False(ProjektPruefdatenKopie.Gleich(stand, p));
    }
}
