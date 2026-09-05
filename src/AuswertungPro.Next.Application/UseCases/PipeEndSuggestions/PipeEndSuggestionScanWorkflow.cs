using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.UseCases.PipeEndSuggestions;

/// <summary>Auftrag fuer einen Rohranfang/Rohrende-Durchlauf aus der Oberflaeche.</summary>
public sealed record PipeEndSuggestionScanWorkflowRequest
{
    public required PipeEndScanRequest Scan { get; init; }

    /// <summary>Haltung, zu der die Liste gehoert — gehoert in jede Meldung.</summary>
    public required string Haltung { get; init; }
}

/// <summary>
/// Die Aussenverbindungen des Durchlaufs. Kein Dateizugriff, keine Modellwahl,
/// keine Regel — nur Zustand, Fortschritt, Abbruch und Klartext-Fehler.
/// </summary>
public sealed record PipeEndSuggestionScanWorkflowActions(
    Func<PipeEndScanRequest, CancellationToken, IProgress<PipeEndScanProgress>?, Task<PipeEndScanResult>> Scan,
    Func<bool> IsBusy,
    Action<bool> SetBusy,
    Func<CancellationToken> ResetCancellation,
    Action<string> SetStatusText,
    Action<string> Log,
    Action<PipeEndScanResult, string> PublishResult);

/// <summary>Ergebnis des Ablaufs (zusaetzlich zu den UI-Aktionen).</summary>
public sealed record PipeEndSuggestionScanWorkflowResult(
    bool Started,
    bool Succeeded,
    PipeEndScanResult? ScanResult,
    string? ErrorMessage);

/// <summary>
/// Orchestriert einen Rohranfang/Rohrende-Durchlauf: Busy-Zustand, Fortschrittstext
/// je Klasse, Abbruch und Fehlermeldung im Klartext. Fehlermeldungen werden
/// woertlich durchgereicht, nicht geglaettet.
/// </summary>
public static class PipeEndSuggestionScanWorkflow
{
    private const string Titel = "Rohranfang/Rohrende";

    public static async Task<PipeEndSuggestionScanWorkflowResult> RunAsync(
        PipeEndSuggestionScanWorkflowRequest request,
        PipeEndSuggestionScanWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (actions.IsBusy())
            return new PipeEndSuggestionScanWorkflowResult(false, false, null, "Ein Durchlauf laeuft bereits.");

        var cancellationToken = actions.ResetCancellation();
        try
        {
            actions.SetBusy(true);
            actions.SetStatusText(
                $"{Titel} {request.Haltung}: Bilder werden extrahiert und geprueft ...");

            // Bewusst synchron auf dem meldenden Thread, nicht Progress<T>: Der
            // Aufrufer marshallt selbst auf die Oberflaeche, und der Test sieht
            // die Meldung sofort.
            var progress = new SynchronousProgress(wert =>
            {
                if (wert.Processed % 25 != 0 && wert.Processed != wert.Total)
                    return;
                actions.SetStatusText(
                    $"{PipeEndKinds.Label(wert.Kind)} {request.Haltung}: Bild {wert.Processed} von {wert.Total} ...");
            });

            var result = await actions.Scan(
                request.Scan, cancellationToken, progress).ConfigureAwait(false);

            var abschluss = $"{Titel} {request.Haltung}: {Zusammenfassung(result)} "
                + $"({result.FramesAnalyzed} Bilder, {result.Duration.TotalSeconds:0} s).";
            actions.SetStatusText(abschluss);
            actions.Log(abschluss);
            actions.PublishResult(result, request.Haltung);
            return new PipeEndSuggestionScanWorkflowResult(true, true, result, null);
        }
        catch (OperationCanceledException)
        {
            var meldung = $"{Titel} {request.Haltung}: abgebrochen.";
            actions.SetStatusText(meldung);
            actions.Log(meldung);
            return new PipeEndSuggestionScanWorkflowResult(true, false, null, "abgebrochen");
        }
        catch (Exception ex)
        {
            var meldung = $"{Titel} {request.Haltung} fehlgeschlagen: {ex.Message}";
            actions.SetStatusText(meldung);
            actions.Log(meldung);
            return new PipeEndSuggestionScanWorkflowResult(true, false, null, ex.Message);
        }
        finally
        {
            actions.SetBusy(false);
        }
    }

    /// <summary>"Rohranfang bei Sekunde 3, kein Rohrende" — je Klasse genau eine Aussage.</summary>
    private static string Zusammenfassung(PipeEndScanResult result)
    {
        var teile = result.Pins
            .Select(pin => pin.Kind)
            .Distinct()
            .OrderBy(kind => kind)
            .Select(kind =>
            {
                var stelle = result.Suggestions.FirstOrDefault(s => s.Kind == kind);
                return stelle is null
                    ? $"kein {PipeEndKinds.Label(kind)}"
                    : $"{PipeEndKinds.Label(kind)} bei Sekunde {(int)Math.Round(stelle.PeakTimeSeconds)}";
            });
        return string.Join(", ", teile);
    }

    private sealed class SynchronousProgress(Action<PipeEndScanProgress> melden) : IProgress<PipeEndScanProgress>
    {
        public void Report(PipeEndScanProgress value) => melden(value);
    }
}
