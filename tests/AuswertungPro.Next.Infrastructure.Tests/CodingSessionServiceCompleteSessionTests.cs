using System;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

// Tests fuer Slice 8a Auto-BCD/BCE/Streckenschaden Step 1 — CompleteSession-
// Overload mit allowOpenStreckenschaden. Mini-ADR:
// docs/adrs/2026-05-10-slice-8a-auto-bcd-bce-strecke.md
//
// Per ADR Q6=C: nur der Service-Overload bekommt einen Test, der Window-
// Dialog ist UI-Smoke-Sache.
public class CodingSessionServiceCompleteSessionTests
{
    private static (CodingSessionService svc, HaltungRecord haltung) StartSessionWithLength(double length = 50.0)
    {
        var haltung = new HaltungRecord();
        haltung.SetFieldValue("Haltungslaenge_m",
            length.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            FieldSource.Manual, userEdited: true);
        haltung.SetFieldValue("Haltungsname", "TEST.001", FieldSource.Manual, userEdited: true);

        var svc = new CodingSessionService();
        svc.StartSession(haltung, videoPath: null);
        return (svc, haltung);
    }

    private static ProtocolEntry NewStreckenschadenEntry(string code, double meterStart)
        => new ProtocolEntry
        {
            Code = code,
            Beschreibung = "Test-Streckenschaden",
            MeterStart = meterStart,
            IsStreckenschaden = true,
            // MeterEnd absichtlich nicht gesetzt -> "offen"
        };

    [Fact]
    public void CompleteSession_NoOpenStreckenschaden_ProducesProtocol()
    {
        var (svc, _) = StartSessionWithLength(50.0);
        // Geschlossener Streckenschaden — MeterEnd > MeterStart.
        var entry = NewStreckenschadenEntry("BAJA", meterStart: 5.0);
        entry.MeterEnd = 8.0;
        svc.AddEvent(entry);

        var doc = svc.CompleteSession();

        Assert.NotNull(doc);
        Assert.NotNull(doc.Current);
        Assert.Contains(doc.Current.Entries, e => e.Code == "BAJA");
    }

    [Fact]
    public void CompleteSession_DefaultBehavior_ThrowsOnOpenStreckenschaden()
    {
        var (svc, _) = StartSessionWithLength();
        svc.AddEvent(NewStreckenschadenEntry("BAJA", meterStart: 5.0));

        var ex = Assert.Throws<InvalidOperationException>(() => svc.CompleteSession());
        Assert.Contains("offene", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompleteSession_AllowOpenFalse_StillThrows()
    {
        var (svc, _) = StartSessionWithLength();
        svc.AddEvent(NewStreckenschadenEntry("BAJA", meterStart: 5.0));

        // Explicit false -> identisch zum Default
        Assert.Throws<InvalidOperationException>(
            () => svc.CompleteSession(allowOpenStreckenschaden: false));
    }

    [Fact]
    public void CompleteSession_AllowOpenTrue_AcceptsAndKeepsOpen()
    {
        var (svc, _) = StartSessionWithLength();
        svc.AddEvent(NewStreckenschadenEntry("BAJA", meterStart: 5.0));

        var doc = svc.CompleteSession(allowOpenStreckenschaden: true);

        Assert.NotNull(doc);
        var streckenschaden = doc.Current.Entries.Single(e => e.Code == "BAJA");
        // Eintrag ist im Protokoll, MeterEnd bleibt ungesetzt (= "offen").
        Assert.Null(streckenschaden.MeterEnd);
        Assert.True(streckenschaden.IsStreckenschaden);
    }

    [Fact]
    public void CompleteSession_AllowOpenTrue_NoOpenStreckenschaden_NormalCompletion()
    {
        // Defensive: allowOpen=true darf den Normal-Pfad nicht stoeren.
        var (svc, _) = StartSessionWithLength();
        var entry = NewStreckenschadenEntry("BAJA", meterStart: 5.0);
        entry.MeterEnd = 8.0;
        svc.AddEvent(entry);

        var doc = svc.CompleteSession(allowOpenStreckenschaden: true);

        Assert.NotNull(doc);
        var ev = doc.Current.Entries.Single(e => e.Code == "BAJA");
        Assert.Equal(8.0, ev.MeterEnd);
    }
}
