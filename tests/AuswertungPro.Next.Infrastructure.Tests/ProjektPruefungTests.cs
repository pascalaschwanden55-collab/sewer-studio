using System.Text.Json;
using AuswertungPro.Next.Application.UseCases.ProjektPruefung;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Projects;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ProjektPruefungTests
{
    private static (Project, HaltungRecord) Haltung()
    {
        var h = new HaltungRecord(); h.SetFieldValue(FieldKeys.HoldingName, "1-2", FieldSource.Manual, true);
        h.SetFieldValue(FieldKeys.HoldingLengthMeters, "10", FieldSource.Manual, true);
        h.Protocol = new() { Current = new() };
        var p = new Project(); p.Data.Add(h); return (p, h);
    }

    [Fact]
    public void Fuenf_Bereiche_mit_stabilen_Zielen_und_ohne_Datenaenderung()
    {
        var (p, h) = Haltung();
        h.SetFieldValue(FieldKeys.Link, "fehlt.mp4", FieldSource.Manual, true);
        h.Protocol!.Current.Entries.Add(new() { Code = "BAB", MeterStart = 12, Ai = new() });
        var s = new SchachtRecord(); p.SchaechteData.Add(s);
        s.SetFieldValue("Tiefe", "-2", FieldSource.Manual, true);
        var feld = FieldCatalog.Objektfelder.Feld("bauwerksteil.bezeichnung");
        var bauteil = new ObjektAkte { Art = "bauwerksteil", Bezuege = [h.Id] };
        p.Objektakten.Add(bauteil);
        var vorher = JsonSerializer.Serialize(p);
        var r = ProjektPruefregeln.Pruefe(p, _ => "Datei fehlt.");
        Assert.Equal(Enum.GetValues<ProjektPruefbereich>().Order(), r.Punkte.Select(x => x.Bereich).Distinct().Order());
        var ki = Assert.Single(r.Punkte.Where(x => x.Bereich == ProjektPruefbereich.KiBefunde));
        Assert.Equal(h.Id, ki.ObjektId); Assert.Equal(h.Protocol.Current.Entries[0].EntryId, ki.EintragId);
        Assert.Contains(r.Punkte, x => x.FeldId == feld.Id && x.ObjektId == h.Id && x.AkteId == bauteil.Id);
        Assert.Contains(r.Punkte, x => x.Bereich == ProjektPruefbereich.Schachthoehen && x.ObjektId == s.Id);
        Assert.Equal(vorher, JsonSerializer.Serialize(p));
    }

    [Theory]
    [InlineData(11, false)]
    [InlineData(11.01, true)]
    [InlineData(-1, true)]
    [InlineData(double.NaN, true)]
    [InlineData(double.PositiveInfinity, true)]
    public void Meterpruefung_beachtet_Toleranz_und_ungueltige_Werte(double meter, bool hinweis)
    {
        var (p, h) = Haltung(); h.Protocol!.Current.Entries.Add(new() { MeterStart = meter });
        var r = ProjektPruefregeln.Pruefe(p, _ => null);
        Assert.Equal(hinweis, r.Punkte.Any(x => x.Bereich == ProjektPruefbereich.Meterangaben));
    }

    [Fact]
    public void Geloeschte_und_bestaetigte_KI_Befunde_sind_nicht_offen()
    {
        var (p, h) = Haltung();
        h.Protocol!.Current.Entries.AddRange([new() { IsDeleted = true, MeterStart = 500, Ai = new() },
            new() { Ai = new() { Accepted = true } }, new() { Code = "BAB" }]);
        Assert.DoesNotContain(ProjektPruefregeln.Pruefe(p, _ => null).Punkte,
            x => x.Bereich is ProjektPruefbereich.KiBefunde or ProjektPruefbereich.Meterangaben);
    }

    [Fact]
    public void Pdf_Listen_werden_aufgeloest_Einzelpfade_behalten_Semikolon()
    {
        var (p, h) = Haltung();
        h.SetFieldValue(FieldKeys.Link, "video;original.mp4", FieldSource.Manual, true);
        h.SetFieldValue(FieldKeys.PdfAll, "[\"a.pdf\",\"b.pdf\"]", FieldSource.Manual, true);
        h.SetFieldValue(FieldKeys.PdfPath, "a.pdf", FieldSource.Manual, true);
        var gelesen = new List<string>();
        ProjektPruefregeln.Pruefe(p, path => { gelesen.Add(path); return null; });
        Assert.Equal(new[] { "video;original.mp4", "a.pdf", "b.pdf" }, gelesen);
    }

    [Fact]
    public void Hoehenpruefung_verwendet_Deckel_und_Original_Sohlenwert()
    {
        var p = new Project(); var s = new SchachtRecord(); p.SchaechteData.Add(s);
        s.SetFieldValue("Tiefe", "3", FieldSource.Manual, true);
        p.Objektakten.Add(new() { Id = s.Id, Art = "schacht", Werte = new() { ["schacht.sohlenhoehe"] = new() { Text = "517" } } });
        p.Objektakten.Add(new() { Art = "deckel", Bezuege = [s.Id], Werte = new() { ["deckel.hoehe"] = new() { Text = "520" } } });
        Assert.DoesNotContain(ProjektPruefregeln.Pruefe(p, _ => null).Punkte, x => x.Bereich == ProjektPruefbereich.Schachthoehen);
        s.SetFieldValue("Tiefe", "4", FieldSource.Manual, true);
        Assert.Contains(ProjektPruefregeln.Pruefe(p, _ => null).Punkte, x => x.Bereich == ProjektPruefbereich.Schachthoehen);
    }

    [Fact]
    public void Abbruch_liefert_keinen_halben_Erfolg()
    {
        var (p, _) = Haltung(); using var cts = new CancellationTokenSource(); cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => ProjektPruefregeln.Pruefe(p, _ => null, cts.Token));
    }

    [Fact]
    public void Dateipruefung_liest_relative_und_externe_Pfade_ohne_zu_schreiben()
    {
        var root = Path.Combine(Path.GetTempPath(), "ProjektPruefung-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Projektdateien"));
        try
        {
            var video = Path.Combine(root, "video.mp4"); File.WriteAllText(video, "video");
            var (p, h) = Haltung(); h.SetFieldValue(FieldKeys.Link, "video.mp4", FieldSource.Manual, true);
            h.SetFieldValue(FieldKeys.PdfPath, "fehlt.pdf", FieldSource.Manual, true);
            var r = new ProjektPruefungService().Pruefe(p, Path.Combine(root, "Projektdateien", "projekt.json"), default);
            var fehler = Assert.Single(r.Punkte.Where(x => x.Bereich == ProjektPruefbereich.Dateien));
            Assert.Contains("fehlt.pdf", fehler.Meldung);
            Assert.Equal("video", File.ReadAllText(video));
            h.SetFieldValue(FieldKeys.Link, video, FieldSource.Manual, true);
            Assert.Single(new ProjektPruefungService().Pruefe(p, null, default).Punkte.Where(x => x.Bereich == ProjektPruefbereich.Dateien));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("../fremd.pdf")]
    [InlineData("C:fremd.pdf")]
    [InlineData("/fremd.pdf")]
    [InlineData("\\\\server\\share\\fremd.pdf")]
    public void Unsichere_Dateipfade_bleiben_sichtbar_ungeprueft(string pfad)
    {
        var (p, h) = Haltung(); h.SetFieldValue(FieldKeys.Link, pfad, FieldSource.Manual, true);
        Assert.Single(new ProjektPruefungService().Pruefe(p, "C:/projekt/projekt.json", default).Punkte.Where(x => x.Bereich == ProjektPruefbereich.Dateien));
    }

    [Fact]
    public void Ohne_absoluten_Projektordner_wird_nicht_im_Arbeitsordner_gesucht()
    {
        var (p, h) = Haltung(); h.SetFieldValue(FieldKeys.Link, "CLAUDE.md", FieldSource.Manual, true);
        var r = new ProjektPruefungService().Pruefe(p, "projekt.json", default);
        Assert.Contains("Ohne gültigen Projektordner", Assert.Single(r.Punkte).Meldung);
    }

    // Deepscan 02.10.2026, R3: Ein Befundfoto im Temp-Ordner galt als in Ordnung, bis Windows
    // aufraeumte (Fall 12.09.2026). Die Pruefung meldet es jetzt am Befund.
    [Fact]
    public void Befundfotos_im_Temp_Ordner_werden_am_Befund_gemeldet()
    {
        var (p, h) = Haltung();
        var e = new ProtocolEntry { Code = "BAB", MeterStart = 2 };
        e.FotoPaths.AddRange([@"C:\Sim\Temp\vsa_foto1_x.png", @"D:\Projekt\Fotos\ok.png"]);
        var geloescht = new ProtocolEntry { Code = "BAC", MeterStart = 3, IsDeleted = true };
        geloescht.FotoPaths.Add(@"C:\Sim\Temp\alt.png");
        h.Protocol!.Current.Entries.AddRange([e, geloescht]);
        var gelesen = new List<string>();

        var r = ProjektPruefregeln.Pruefe(p, pfad => { gelesen.Add(pfad); return null; }, default, [@"C:\Sim\Temp"]);

        var punkt = Assert.Single(r.Punkte.Where(x => x.Bereich == ProjektPruefbereich.Dateien));
        Assert.Equal(e.EntryId, punkt.EintragId);
        Assert.Equal(h.Id, punkt.ObjektId);
        Assert.Contains(@"C:\Sim\Temp\vsa_foto1_x.png", punkt.Meldung);
        Assert.Contains("liegt im Temp-Ordner", punkt.Meldung);
        Assert.Equal([@"C:\Sim\Temp\vsa_foto1_x.png"], gelesen);
    }

    [Fact]
    public void Fehlendes_Befundfoto_aus_dem_Temp_Ordner_nennt_beides()
    {
        var (p, h) = Haltung();
        var e = new ProtocolEntry { Code = "BAB", MeterStart = 2 };
        e.FotoPaths.Add(@"C:\Sim\Temp\weg.png");
        h.Protocol!.Current.Entries.Add(e);

        var r = ProjektPruefregeln.Pruefe(p, _ => "Datei fehlt.", default, [@"C:\Sim\Temp"]);

        var meldung = Assert.Single(r.Punkte.Where(x => x.Bereich == ProjektPruefbereich.Dateien)).Meldung;
        Assert.Contains("Datei fehlt.", meldung);
        Assert.Contains("Temp-Ordner", meldung);
    }

    [Fact]
    public void Dienst_meldet_ein_vorhandenes_Befundfoto_im_echten_Temp_Ordner()
    {
        var foto = Path.Combine(Path.GetTempPath(), "vsa_foto1_" + Guid.NewGuid().ToString("N") + ".png");
        File.WriteAllBytes(foto, [1, 2, 3]);
        try
        {
            var (p, h) = Haltung();
            var e = new ProtocolEntry { Code = "BAB", MeterStart = 2 };
            e.FotoPaths.Add(foto);
            h.Protocol!.Current.Entries.Add(e);

            var r = new ProjektPruefungService().Pruefe(p, null, default);

            var punkt = Assert.Single(r.Punkte.Where(x => x.Bereich == ProjektPruefbereich.Dateien));
            Assert.Equal(e.EntryId, punkt.EintragId);
            Assert.Contains("liegt im Temp-Ordner", punkt.Meldung);
            Assert.True(File.Exists(foto), "Die Prüfung darf nichts verändern.");
        }
        finally { File.Delete(foto); }
    }

    [Fact]
    public void Werte_ohne_webgis_begriff_und_nicht_sendbare_masse_werden_gemeldet()
    {
        var projekt = new Project();
        var h = new HaltungRecord();
        h.SetFieldValue(FieldKeys.HoldingName, "H1", FieldSource.Manual, false);
        h.Fields[FieldKeys.OperatingStatus] = "weitere";
        h.Fields[FieldKeys.NominalDiameterMm] = "147"; // 148 und 150 fuehrt das WebGIS, 147 nicht
        projekt.Data.Add(h);
        var s = new SchachtRecord();
        s.Fields["Schachtnummer"] = "80409";
        s.Fields["Funktion"] = "Absturzbauwerk";
        projekt.SchaechteData.Add(s);

        var punkte = ProjektPruefregeln.Pruefe(projekt, _ => null).Punkte;

        Assert.Contains(punkte, p => p.Objektname == "H1" && p.Meldung.Contains("«weitere»") && p.Meldung.Contains("kein WebGIS-Begriff"));
        Assert.Contains(punkte, p => p.Objektname == "H1" && p.Meldung.Contains("«147»") && p.Meldung.Contains("nicht in der WebGIS-Liste"));
        Assert.Contains(punkte, p => p.Objektname == "80409" && p.Meldung.Contains("«Absturzbauwerk»"));
    }
}
