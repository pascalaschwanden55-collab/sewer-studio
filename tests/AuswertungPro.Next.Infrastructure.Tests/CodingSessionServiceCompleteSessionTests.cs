using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AuswertungPro.Next.Application.Ai;
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
//
// CompleteSession ruft intern PersistTrainingSamplesFromEvents, was
// ueber TrainingSamplesStore -> KnowledgeRootProvider geht. Der Static-
// Resolver wird per StoreIsolation pro Test umgeleitet. Damit das Set/
// Read nicht mit anderen Tests racet, die den gleichen statischen
// Resolver anfassen (TrainingSamplesWriterAdapterTests), serialisieren
// wir per [Collection].
[Collection("KnowledgeRootIsolation")]
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

    /// <summary>Lenkt KnowledgeRoot auf einen frischen Temp-Pfad, damit
    /// CompleteSession's PersistTrainingSamplesFromEvents nicht die produktiven
    /// training_samples.json oder andere Test-Stores anfasst. Identisch zu
    /// StoreIsolation in TrainingSamplesWriterAdapterTests; dort ist die Klasse
    /// privat — hier dupliziert statt Sichtbarkeit zu erweitern.</summary>
    private sealed class StoreIsolation : IDisposable
    {
        private readonly string _tempDir;
        private readonly Func<string>? _previousResolver;

        private StoreIsolation(string tempDir, Func<string>? previousResolver)
        {
            _tempDir = tempDir;
            _previousResolver = previousResolver;
        }

        public static StoreIsolation Fresh()
        {
            var previousResolver = ReadStaticResolver();
            var tempDir = Path.Combine(
                Path.GetTempPath(),
                "CodingSessionServiceCompleteSessionTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            KnowledgeRootProvider.SetResolver(() => tempDir);
            return new StoreIsolation(tempDir, previousResolver);
        }

        public void Dispose()
        {
            WriteStaticResolver(_previousResolver);
            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, recursive: true);
            }
            catch { /* best-effort */ }
        }

        private static FieldInfo GetField()
            => typeof(KnowledgeRootProvider).GetField(
                "_resolver",
                BindingFlags.NonPublic | BindingFlags.Static)
               ?? throw new InvalidOperationException("KnowledgeRootProvider._resolver missing");

        private static Func<string>? ReadStaticResolver()
            => GetField().GetValue(null) as Func<string>;

        private static void WriteStaticResolver(Func<string>? resolver)
            => GetField().SetValue(null, resolver);
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
        using var iso = StoreIsolation.Fresh();
        var (svc, _) = StartSessionWithLength(50.0);
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
        using var iso = StoreIsolation.Fresh();
        var (svc, _) = StartSessionWithLength();
        svc.AddEvent(NewStreckenschadenEntry("BAJA", meterStart: 5.0));

        var ex = Assert.Throws<InvalidOperationException>(() => svc.CompleteSession());
        Assert.Contains("offene", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompleteSession_AllowOpenFalse_StillThrows()
    {
        using var iso = StoreIsolation.Fresh();
        var (svc, _) = StartSessionWithLength();
        svc.AddEvent(NewStreckenschadenEntry("BAJA", meterStart: 5.0));

        Assert.Throws<InvalidOperationException>(
            () => svc.CompleteSession(allowOpenStreckenschaden: false));
    }

    [Fact]
    public void CompleteSession_AllowOpenTrue_AcceptsAndKeepsOpen()
    {
        using var iso = StoreIsolation.Fresh();
        var (svc, _) = StartSessionWithLength();
        svc.AddEvent(NewStreckenschadenEntry("BAJA", meterStart: 5.0));

        var doc = svc.CompleteSession(allowOpenStreckenschaden: true);

        Assert.NotNull(doc);
        var streckenschaden = doc.Current.Entries.Single(e => e.Code == "BAJA");
        Assert.Null(streckenschaden.MeterEnd);
        Assert.True(streckenschaden.IsStreckenschaden);
    }

    [Fact]
    public void CompleteSession_AllowOpenTrue_NoOpenStreckenschaden_NormalCompletion()
    {
        using var iso = StoreIsolation.Fresh();
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
