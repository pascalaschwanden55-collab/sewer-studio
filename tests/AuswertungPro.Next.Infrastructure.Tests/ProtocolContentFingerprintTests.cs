using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import.Common;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Sichert Audit I1: Re-Import derselben Quelle mit identischem Inhalt darf keine
/// neue Protokoll-Revision erzeugen (KINS/IBAK/WinCan historisierten bisher bei
/// JEDEM Re-Import — die History wuchs um inhaltsgleiche Kopien).
/// </summary>
public sealed class ProtocolContentFingerprintTests
{
    private static ProtocolEntry Entry(
        string code, double? meterStart, string beschreibung = "",
        double? meterEnd = null, bool strecke = false, string? mpeg = null,
        TimeSpan? zeit = null, bool deleted = false, params string[] fotos)
    {
        return new ProtocolEntry
        {
            Code = code,
            Beschreibung = beschreibung,
            MeterStart = meterStart,
            MeterEnd = meterEnd,
            IsStreckenschaden = strecke,
            Mpeg = mpeg,
            Zeit = zeit,
            IsDeleted = deleted,
            FotoPaths = new List<string>(fotos)
        };
    }

    private static ProtocolRevision Revision(params ProtocolEntry[] entries)
        => new() { Entries = new List<ProtocolEntry>(entries) };

    [Fact]
    public void Identischer_Inhalt_mit_neuen_EntryIds_gilt_als_gleich()
    {
        // Jeder Import erzeugt neue GUIDs — die duerfen keinen Unterschied machen.
        var current = Revision(Entry("BAB", 12.50, "Riss laengs"), Entry("BCE", 47.90));
        var incoming = new List<ProtocolEntry> { Entry("BAB", 12.50, "Riss laengs"), Entry("BCE", 47.90) };

        Assert.True(ProtocolContentFingerprint.HasSameContent(current, incoming));
    }

    [Fact]
    public void Reihenfolge_spielt_keine_Rolle()
    {
        var current = Revision(Entry("BAB", 12.50), Entry("BCE", 47.90));
        var incoming = new List<ProtocolEntry> { Entry("BCE", 47.90), Entry("BAB", 12.50) };

        Assert.True(ProtocolContentFingerprint.HasSameContent(current, incoming));
    }

    [Fact]
    public void Umgeschriebene_Fotopfade_gelten_als_gleich_solange_Anzahl_stimmt()
    {
        // MediaDistributionService schreibt FotoPaths nach dem Import auf
        // projekt-relative Pfade um — das ist KEINE inhaltliche Aenderung.
        var current = Revision(Entry("BCA", 15.60, fotos: @"Haltungen\X\Fotos\bild_064.jpg"));
        var incoming = new List<ProtocolEntry> { Entry("BCA", 15.60, fotos: @"D:\Export\bild_064.jpg") };

        Assert.True(ProtocolContentFingerprint.HasSameContent(current, incoming));
    }

    [Fact]
    public void Neues_Foto_gilt_als_Aenderung()
    {
        var current = Revision(Entry("BCA", 15.60, fotos: "a.jpg"));
        var incoming = new List<ProtocolEntry> { Entry("BCA", 15.60, fotos: new[] { "a.jpg", "b.jpg" }) };

        Assert.False(ProtocolContentFingerprint.HasSameContent(current, incoming));
    }

    [Theory]
    [InlineData("anderer Meter")]
    [InlineData("anderer Code")]
    [InlineData("neuer Eintrag")]
    [InlineData("Eintrag fehlt")]
    [InlineData("Loeschmarker")]
    public void Inhaltliche_Aenderungen_werden_erkannt(string fall)
    {
        var current = Revision(Entry("BAB", 12.50, "Riss laengs"), Entry("BCE", 47.90));

        var incoming = fall switch
        {
            "anderer Meter" => new List<ProtocolEntry> { Entry("BAB", 12.60, "Riss laengs"), Entry("BCE", 47.90) },
            "anderer Code" => new List<ProtocolEntry> { Entry("BAC", 12.50, "Riss laengs"), Entry("BCE", 47.90) },
            "neuer Eintrag" => new List<ProtocolEntry> { Entry("BAB", 12.50, "Riss laengs"), Entry("BCE", 47.90), Entry("BBA", 30.00) },
            "Eintrag fehlt" => new List<ProtocolEntry> { Entry("BAB", 12.50, "Riss laengs") },
            "Loeschmarker" => new List<ProtocolEntry> { Entry("BAB", 12.50, "Riss laengs", deleted: true), Entry("BCE", 47.90) },
            _ => throw new InvalidOperationException(fall)
        };

        Assert.False(ProtocolContentFingerprint.HasSameContent(current, incoming));
    }

    [Fact]
    public void Streckenschaden_und_MeterEnd_zaehlen_zum_Inhalt()
    {
        var current = Revision(Entry("BAF", 2.50, meterEnd: 8.00, strecke: true));
        var samePoint = new List<ProtocolEntry> { Entry("BAF", 2.50) };

        Assert.False(ProtocolContentFingerprint.HasSameContent(current, samePoint));
    }

    [Fact]
    public void Leere_Revision_und_leerer_Import_gelten_als_gleich()
    {
        Assert.True(ProtocolContentFingerprint.HasSameContent(Revision(), new List<ProtocolEntry>()));
    }

    [Fact]
    public void Null_Revision_gilt_nie_als_gleich()
    {
        Assert.False(ProtocolContentFingerprint.HasSameContent(null, new List<ProtocolEntry>()));
    }
}
