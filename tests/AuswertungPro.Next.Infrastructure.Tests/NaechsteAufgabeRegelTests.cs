using AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class NaechsteAufgabeRegelTests
{
    private static HaltungRecord Haltung(string name, string status = "", string link = "", bool offenerKiBefund = false)
    {
        var r = new HaltungRecord();
        r.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Manual, false);
        r.SetFieldValue(FieldKeys.WorkflowStatus, status, FieldSource.Manual, false);
        r.SetFieldValue(FieldKeys.Link, link, FieldSource.Manual, false);
        if (offenerKiBefund)
        {
            r.Protocol = new ProtocolDocument();
            r.Protocol.Current ??= new ProtocolRevision();
            r.Protocol.Current.Entries.Add(new ProtocolEntry { Code = "BAB", Ai = new ProtocolEntryAiMeta { Accepted = false, Confidence = 0.9 } });
        }
        return r;
    }

    [Fact]
    public void Abgeschlossen_schlaegt_offene_KI_Befunde()
        => Assert.Equal(HaltungPruefstand.Abgeschlossen, HaltungPruefstatus.Bestimme(Haltung("a", "abgeschlossen", offenerKiBefund: true)));

    [Fact]
    public void Offener_KI_Befund_heisst_KI_analysiert()
        => Assert.Equal(HaltungPruefstand.KiAnalysiert, HaltungPruefstatus.Bestimme(Haltung("a", offenerKiBefund: true)));

    [Fact]
    public void Ohne_alles_ist_offen()
        => Assert.Equal(HaltungPruefstand.Offen, HaltungPruefstatus.Bestimme(Haltung("a")));

    [Fact]
    public void Naechste_ist_zuerst_KI_analysiert_dann_offen_mit_Video()
    {
        var ohneVideo = Haltung("1-2");
        var mitVideo = Haltung("2-3", link: "v.mp4");
        var analysiert = Haltung("3-4", offenerKiBefund: true);
        Assert.Same(analysiert, NaechsteAufgabeRegel.Naechste(new[] { ohneVideo, mitVideo, analysiert }));
        Assert.Same(mitVideo, NaechsteAufgabeRegel.Naechste(new[] { ohneVideo, mitVideo }));
        Assert.Null(NaechsteAufgabeRegel.Naechste(new[] { ohneVideo, Haltung("9-9", "abgeschlossen", "v.mp4") }));
    }

    [Fact]
    public void Chiptext_nennt_die_Haltung_oder_keine_offene_Pruefung()
    {
        Assert.Equal("Nächste Aufgabe: 2-3 prüfen", NaechsteAufgabeRegel.ChipText(Haltung("2-3", link: "v.mp4")));
        Assert.Equal("Keine offene Prüfung", NaechsteAufgabeRegel.ChipText(null));
    }

    [Theory]
    [InlineData(HaltungPruefstand.Abgeschlossen, "fachlich geprüft")]
    [InlineData(HaltungPruefstand.KiAnalysiert, "KI analysiert, Prüfung offen")]
    [InlineData(HaltungPruefstand.Offen, "nicht analysiert")]
    public void Statustexte_entsprechen_dem_Prototyp(HaltungPruefstand stand, string text)
        => Assert.Equal(text, HaltungPruefstatus.Text(stand));
}
