using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Der Workflow um den Durchlauf: Busy-Zustaende, Fortschritt, Abbruch und
/// Klartext-Fehler. Der Durchlauf selbst ist hier eine Nachbildung — geprueft
/// wird nur die Orchestrierung.
/// </summary>
public sealed class BendSuggestionScanWorkflowTests
{
    private const string Haltung = "36053-36052";

    [Fact]
    public async Task Bei_laufendem_Durchlauf_startet_kein_zweiter()
    {
        var scanAufgerufen = false;
        var aktionen = Aktionen(scan: (_, _, _) =>
        {
            scanAufgerufen = true;
            return Task.FromResult(Ergebnis(1));
        }) with { IsBusy = () => true };

        var result = await BendSuggestionScanWorkflow.RunAsync(Auftrag(), aktionen);

        Assert.False(result.Started);
        Assert.False(scanAufgerufen);
    }

    [Fact]
    public async Task Ein_Erfolg_veroeffentlicht_die_Liste_zur_Haltung()
    {
        var veroeffentlicht = new List<(BendSuggestionScanResult Ergebnis, string Haltung)>();
        var statusTexte = new List<string>();
        var busyVerlauf = new List<bool>();
        var aktionen = Aktionen(
            scan: (_, _, fortschritt) =>
            {
                fortschritt?.Report(new BendSuggestionScanProgress(50, 100));
                fortschritt?.Report(new BendSuggestionScanProgress(100, 100));
                return Task.FromResult(Ergebnis(2));
            },
            veroeffentliche: (ergebnis, haltung) => veroeffentlicht.Add((ergebnis, haltung)),
            status: statusTexte.Add,
            busy: busyVerlauf.Add);

        // Progress<T> posted auf den SynchronizationContext — im echten Betrieb der
        // UI-Thread. Im Test gibt es keinen; ohne ihn kaemen die Meldungen asynchron
        // aus dem Threadpool und koennten erst nach dem Abschluss ankommen.
        var vorherigerKontext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new SofortSynchronKontext());
        BendSuggestionScanWorkflowResult result;
        try
        {
            result = await BendSuggestionScanWorkflow.RunAsync(Auftrag(), aktionen);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(vorherigerKontext);
        }

        Assert.True(result.Started);
        Assert.True(result.Succeeded);
        var einziger = Assert.Single(veroeffentlicht);
        Assert.Equal(Haltung, einziger.Haltung);
        Assert.Equal(2, einziger.Ergebnis.Suggestions.Count);
        Assert.Equal(new[] { true, false }, busyVerlauf);
        Assert.Contains(statusTexte, text => text.Contains("Bild 50 von 100"));
        Assert.Contains(statusTexte, text => text.Contains("2 Stellen"));
    }

    /// <summary>Fuehrt Posts sofort und synchron aus — nur fuer Tests ohne UI-Thread.</summary>
    private sealed class SofortSynchronKontext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => d(state);

        public override void Send(SendOrPostCallback d, object? state) => d(state);
    }

    [Fact]
    public async Task Ohne_Arbeitspunkt_erscheint_die_Policy_Meldung_und_keine_Liste()
    {
        var veroeffentlicht = false;
        var statusTexte = new List<string>();
        var aktionen = Aktionen(
            scan: (_, _, _) => Task.FromResult(new BendSuggestionScanResult(
                false,
                "Kein kalibrierter Arbeitspunkt fuer diesen Kandidaten hinterlegt.",
                Array.Empty<BendSuggestion>(), 0, 0, TimeSpan.Zero,
                "bcc_x", "abc", 0.0, 0.0)),
            veroeffentliche: (_, _) => veroeffentlicht = true,
            status: statusTexte.Add);

        var result = await BendSuggestionScanWorkflow.RunAsync(Auftrag(), aktionen);

        Assert.True(result.Started);
        Assert.False(result.Succeeded);
        Assert.False(veroeffentlicht);
        Assert.Contains(statusTexte, text => text.Contains("Arbeitspunkt"));
    }

    [Fact]
    public async Task Ein_technischer_Fehler_wird_woertlich_durchgereicht()
    {
        // "ffmpeg ist fehlgeschlagen: moov atom not found" sagt dem Benutzer,
        // dass die Datei defekt ist — nicht glaetten.
        var statusTexte = new List<string>();
        var aktionen = Aktionen(
            scan: (_, _, _) => throw new InvalidOperationException(
                "ffmpeg ist fehlgeschlagen: moov atom not found"),
            status: statusTexte.Add);

        var result = await BendSuggestionScanWorkflow.RunAsync(Auftrag(), aktionen);

        Assert.True(result.Started);
        Assert.False(result.Succeeded);
        Assert.Equal("ffmpeg ist fehlgeschlagen: moov atom not found", result.ErrorMessage);
        Assert.Contains(statusTexte, text => text.Contains("moov atom not found"));
    }

    [Fact]
    public async Task Ein_Abbruch_bleibt_ein_Abbruch_und_raeumt_auf()
    {
        var busyVerlauf = new List<bool>();
        var statusTexte = new List<string>();
        var aktionen = Aktionen(
            scan: (_, _, _) => throw new OperationCanceledException(),
            status: statusTexte.Add,
            busy: busyVerlauf.Add);

        var result = await BendSuggestionScanWorkflow.RunAsync(Auftrag(), aktionen);

        Assert.True(result.Started);
        Assert.False(result.Succeeded);
        Assert.Equal("abgebrochen", result.ErrorMessage);
        Assert.Contains(statusTexte, text => text.Contains("abgebrochen"));
        Assert.Equal(new[] { true, false }, busyVerlauf);
    }

    private static BendSuggestionScanWorkflowRequest Auftrag() => new()
    {
        Scan = new BendSuggestionScanRequest
        {
            VideoPath = @"D:\Videos\H_36053-36052.mpg",
            CandidateId = "bcc_nc15_seed46_20260808",
            WeightSha256 = new string('a', 64)
        },
        Haltung = Haltung
    };

    private static BendSuggestionScanResult Ergebnis(int stellen)
    {
        var vorschlaege = new List<BendSuggestion>();
        for (var index = 0; index < stellen; index++)
        {
            vorschlaege.Add(new BendSuggestion(
                0.2 + index, 1.0 + index, 35.0, 0.87, 12, BendSuggestionStrength.Strong));
        }
        return new BendSuggestionScanResult(
            true, string.Empty, vorschlaege, 100, 0, TimeSpan.FromSeconds(42),
            "bcc_nc15_seed46_20260808", new string('a', 64), 0.50, 0.80);
    }

    private static BendSuggestionScanWorkflowActions Aktionen(
        Func<BendSuggestionScanRequest, CancellationToken, IProgress<BendSuggestionScanProgress>?,
            Task<BendSuggestionScanResult>> scan,
        Action<BendSuggestionScanResult, string>? veroeffentliche = null,
        Action<string>? status = null,
        Action<bool>? busy = null)
        => new(
            Scan: scan,
            IsBusy: () => false,
            SetBusy: busy ?? (_ => { }),
            ResetCancellation: () => CancellationToken.None,
            SetStatusText: status ?? (_ => { }),
            Log: _ => { },
            PublishResult: veroeffentliche ?? ((_, _) => { }));
}
