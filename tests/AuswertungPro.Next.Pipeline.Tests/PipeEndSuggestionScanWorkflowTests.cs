using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.UseCases.PipeEndSuggestions;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Orchestrierung des Rohranfang/Rohrende-Durchlaufs aus der Oberflaeche:
/// Busy-Zustand, Fortschrittstext je Klasse, Abbruch und Klartext-Fehler.
/// Kein Dateizugriff, keine Modellwahl, keine Regel — nur Zustand und Meldungen.
/// </summary>
public sealed class PipeEndSuggestionScanWorkflowTests
{
    private const string Haltung = "36053-36052";

    [Fact]
    public async Task Ein_erfolgreicher_Lauf_setzt_Busy_veroeffentlicht_und_nennt_beide_Stellen()
    {
        var geruest = new Geruest
        {
            Ergebnis = Ergebnis(
                new PipeEndSuggestion(PipeEndKind.Rohranfang, 2.0, 4.0, 3.0, 0.97, 3),
                new PipeEndSuggestion(PipeEndKind.Rohrende, 212.0, 216.0, 214.4, 0.99, 5))
        };

        var lauf = await PipeEndSuggestionScanWorkflow.RunAsync(Auftrag(), geruest.Aktionen());

        Assert.True(lauf.Started);
        Assert.True(lauf.Succeeded);
        Assert.Same(geruest.Ergebnis, lauf.ScanResult);
        Assert.Equal(new[] { true, false }, geruest.Busy);
        var (veroeffentlicht, haltung) = Assert.Single(geruest.Veroeffentlicht);
        Assert.Same(geruest.Ergebnis, veroeffentlicht);
        Assert.Equal(Haltung, haltung);
        Assert.Contains("Rohranfang bei Sekunde 3", geruest.LetzterStatus);
        Assert.Contains("Rohrende bei Sekunde 214", geruest.LetzterStatus);
        Assert.Contains("550 Bilder", geruest.LetzterStatus);
        Assert.Contains(geruest.LetzterStatus, geruest.Protokoll);
    }

    [Fact]
    public async Task Ohne_Treffer_nennt_der_Abschluss_kein_Rohranfang_und_kein_Rohrende()
    {
        // Eine leere Liste ist ein gueltiges Ergebnis und wird trotzdem veroeffentlicht:
        // Ab dem Anzeigen gilt die folgende Codierung als beeinflusst.
        var geruest = new Geruest { Ergebnis = Ergebnis() };

        var lauf = await PipeEndSuggestionScanWorkflow.RunAsync(Auftrag(), geruest.Aktionen());

        Assert.True(lauf.Succeeded);
        Assert.Single(geruest.Veroeffentlicht);
        Assert.Contains("kein Rohranfang", geruest.LetzterStatus);
        Assert.Contains("kein Rohrende", geruest.LetzterStatus);
    }

    [Fact]
    public async Task Bei_laufendem_Durchlauf_startet_kein_zweiter()
    {
        var geruest = new Geruest { IstBusy = true };

        var lauf = await PipeEndSuggestionScanWorkflow.RunAsync(Auftrag(), geruest.Aktionen());

        Assert.False(lauf.Started);
        Assert.Empty(geruest.Anfragen);
        Assert.Empty(geruest.Busy);
    }

    [Fact]
    public async Task Ein_Fehler_wird_woertlich_gemeldet_und_Busy_wieder_freigegeben()
    {
        var geruest = new Geruest
        {
            Fehler = new InvalidOperationException("Keine freigegebene Lernstufe 'rohranfang' mit diesem Hash.")
        };

        var lauf = await PipeEndSuggestionScanWorkflow.RunAsync(Auftrag(), geruest.Aktionen());

        Assert.True(lauf.Started);
        Assert.False(lauf.Succeeded);
        Assert.Equal("Keine freigegebene Lernstufe 'rohranfang' mit diesem Hash.", lauf.ErrorMessage);
        Assert.Contains("Keine freigegebene Lernstufe", geruest.LetzterStatus);
        Assert.Equal(new[] { true, false }, geruest.Busy);
        Assert.Empty(geruest.Veroeffentlicht);
    }

    [Fact]
    public async Task Ein_Abbruch_wird_als_abgebrochen_gemeldet()
    {
        var geruest = new Geruest { Fehler = new OperationCanceledException() };

        var lauf = await PipeEndSuggestionScanWorkflow.RunAsync(Auftrag(), geruest.Aktionen());

        Assert.False(lauf.Succeeded);
        Assert.Equal("abgebrochen", lauf.ErrorMessage);
        Assert.Contains("abgebrochen", geruest.LetzterStatus);
        Assert.Empty(geruest.Veroeffentlicht);
    }

    [Fact]
    public async Task Der_Fortschritt_nennt_die_Klasse_und_das_Bild()
    {
        var geruest = new Geruest
        {
            Ergebnis = Ergebnis(),
            Fortschritt = [new PipeEndScanProgress(PipeEndKind.Rohrende, 25, 550)]
        };

        await PipeEndSuggestionScanWorkflow.RunAsync(Auftrag(), geruest.Aktionen());

        Assert.Contains(geruest.Statusmeldungen, text => text.Contains("Rohrende") && text.Contains("Bild 25 von 550"));
    }

    private static PipeEndSuggestionScanWorkflowRequest Auftrag()
        => new()
        {
            Scan = new PipeEndScanRequest { VideoPath = @"D:\Haltungen\H_36053-36052.mpg" },
            Haltung = Haltung
        };

    private static PipeEndScanResult Ergebnis(params PipeEndSuggestion[] stellen)
        => new(stellen, FramesAnalyzed: 550, Duration: TimeSpan.FromSeconds(40), Pins: PipeEndLernstufePins.All);

    private sealed class Geruest
    {
        public bool IstBusy { get; set; }
        public PipeEndScanResult? Ergebnis { get; set; }
        public Exception? Fehler { get; set; }
        public List<PipeEndScanProgress> Fortschritt { get; set; } = [];
        public List<bool> Busy { get; } = [];
        public List<PipeEndScanRequest> Anfragen { get; } = [];
        public List<string> Statusmeldungen { get; } = [];
        public List<string> Protokoll { get; } = [];
        public List<(PipeEndScanResult Ergebnis, string Haltung)> Veroeffentlicht { get; } = [];
        public string LetzterStatus => Statusmeldungen.Count == 0 ? string.Empty : Statusmeldungen[^1];

        public PipeEndSuggestionScanWorkflowActions Aktionen() => new(
            Scan: (anfrage, _, fortschritt) =>
            {
                Anfragen.Add(anfrage);
                foreach (var meldung in Fortschritt)
                    fortschritt?.Report(meldung);
                if (Fehler is not null)
                    return Task.FromException<PipeEndScanResult>(Fehler);
                return Task.FromResult(Ergebnis ?? throw new InvalidOperationException("Testgeruest ohne Ergebnis."));
            },
            IsBusy: () => IstBusy,
            SetBusy: wert => { IstBusy = wert; Busy.Add(wert); },
            ResetCancellation: () => CancellationToken.None,
            SetStatusText: Statusmeldungen.Add,
            Log: Protokoll.Add,
            PublishResult: (ergebnis, haltung) => Veroeffentlicht.Add((ergebnis, haltung)));
    }
}
