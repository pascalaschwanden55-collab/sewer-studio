using System;
using System.Linq;
using AuswertungPro.Next.Application.Kostenanalyse;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Kostenanalyse;

public sealed class KostenfallExtraktorTests
{
    private static readonly DateTime Zeitpunkt = new(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc);

    private static HaltungRecord Haltung(string name, string dn, string laenge, params string[] codes)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", name, FieldSource.Manual, userEdited: false);
        record.SetFieldValue("DN_mm", dn, FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Haltungslaenge_m", laenge, FieldSource.Manual, userEdited: false);
        record.Protocol = new ProtocolDocument
        {
            Current = new ProtocolRevision
            {
                Entries = [.. codes.Select(c => new ProtocolEntry { Code = c })]
            }
        };
        return record;
    }

    private static HoldingCost Kosten(decimal menge = 40m) => new()
    {
        Holding = "H-1",
        Measures =
        [
            new MeasureCost
            {
                MeasureId = "M", MeasureName = "Renovierung",
                Lines = [new CostLine { ItemKey = "SCHLAUCHLINER_GFK", Qty = menge, Unit = "m", UnitPrice = 200m, Selected = true }]
            }
        ]
    };

    [Fact]
    public void Erstellt_einen_Fall_aus_Haltung_und_Kosten()
    {
        var ok = KostenfallExtraktor.TryErstellen(
            Haltung("H-1", "300", "40", "BAF01"), Kosten(), "Zone 1.15",
            KostenfallHerkunft.Unbeeinflusst, Zeitpunkt, out var fall, out var grund);

        Assert.True(ok, grund);
        Assert.Equal("H-1", fall!.Haltung);
        Assert.Equal("Zone 1.15", fall.Projekt);
        Assert.Equal(Zeitpunkt, fall.ErfasstUtc);
        Assert.Equal(300, fall.Merkmale.DnMm);
        Assert.Equal("SCHLAUCHLINER_GFK", Assert.Single(fall.Positionen).ItemKey);
    }

    [Fact]
    public void Ohne_Durchmesser_kein_Fall()
    {
        var ok = KostenfallExtraktor.TryErstellen(
            Haltung("H-1", "", "40", "BAF01"), Kosten(), "P",
            KostenfallHerkunft.Unbeeinflusst, Zeitpunkt, out _, out var grund);

        Assert.False(ok);
        Assert.Contains("Durchmesser", grund);
    }

    [Fact]
    public void Ohne_Laenge_kein_Fall()
    {
        var ok = KostenfallExtraktor.TryErstellen(
            Haltung("H-1", "300", "0", "BAF01"), Kosten(), "P",
            KostenfallHerkunft.Unbeeinflusst, Zeitpunkt, out _, out var grund);

        Assert.False(ok);
        Assert.Contains("Laenge", grund);
    }

    [Fact]
    public void Ohne_Schaeden_kein_Fall()
    {
        var ok = KostenfallExtraktor.TryErstellen(
            Haltung("H-1", "300", "40", "BCD", "BCE"), Kosten(), "P",
            KostenfallHerkunft.Unbeeinflusst, Zeitpunkt, out _, out var grund);

        Assert.False(ok);
        Assert.Contains("Schaden", grund);
    }

    [Fact]
    public void Ohne_Massnahmen_kein_Fall()
    {
        var ok = KostenfallExtraktor.TryErstellen(
            Haltung("H-1", "300", "40", "BAF01"), new HoldingCost { Holding = "H-1" }, "P",
            KostenfallHerkunft.Unbeeinflusst, Zeitpunkt, out _, out var grund);

        Assert.False(ok);
        Assert.Contains("Massnahme", grund);
    }

    [Fact]
    public void Ohne_Haltungsnamen_kein_Fall()
    {
        var ok = KostenfallExtraktor.TryErstellen(
            Haltung("", "300", "40", "BAF01"), Kosten(), "P",
            KostenfallHerkunft.Unbeeinflusst, Zeitpunkt, out _, out var grund);

        Assert.False(ok);
        Assert.Contains("Haltungsname", grund);
    }

    [Fact]
    public void Die_Herkunft_wird_uebernommen()
    {
        KostenfallExtraktor.TryErstellen(
            Haltung("H-1", "300", "40", "BAF01"), Kosten(), "P",
            KostenfallHerkunft.VorschlagGesehen, Zeitpunkt, out var fall, out _);

        Assert.Equal(KostenfallHerkunft.VorschlagGesehen, fall!.Herkunft);
    }
}
