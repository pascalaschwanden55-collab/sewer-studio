namespace AuswertungPro.Next.Application.UseCases.BendSuggestions;

/// <summary>Auftrag fuer einen Bogen-Vorschlagsdurchlauf aus der Oberflaeche.</summary>
public sealed record BendSuggestionScanWorkflowRequest
{
    public required BendSuggestionScanRequest Scan { get; init; }

    /// <summary>Haltung, zu der die Liste gehoert — gehoert in jede Meldung.</summary>
    public required string Haltung { get; init; }
}

/// <summary>
/// Die Aussenverbindungen des Durchlaufs. Kein Dateizugriff, keine Modellwahl,
/// keine Aggregation — nur Zustand, Fortschritt, Abbruch und Klartext-Fehler.
/// </summary>
public sealed record BendSuggestionScanWorkflowActions(
    Func<BendSuggestionScanRequest, CancellationToken, IProgress<BendSuggestionScanProgress>?, Task<BendSuggestionScanResult>> Scan,
    Func<bool> IsBusy,
    Action<bool> SetBusy,
    Func<CancellationToken> ResetCancellation,
    Action<string> SetStatusText,
    Action<string> Log,
    Action<BendSuggestionScanResult, string> PublishResult);

/// <summary>Ergebnis des Ablaufs (zusaetzlich zu den UI-Aktionen).</summary>
public sealed record BendSuggestionScanWorkflowResult(
    bool Started,
    bool Succeeded,
    BendSuggestionScanResult? ScanResult,
    string? ErrorMessage);

/// <summary>
/// Orchestriert einen Bogen-Vorschlagsdurchlauf: Busy-Zustand, Fortschrittstext,
/// Abbruch und Fehlermeldung im Klartext. Fehlermeldungen werden woertlich
/// durchgereicht, nicht geglaettet — "ffmpeg ist fehlgeschlagen: moov atom not
/// found" sagt dem Benutzer, dass die Datei defekt ist.
/// </summary>
public static class BendSuggestionScanWorkflow
{
    public static async Task<BendSuggestionScanWorkflowResult> RunAsync(
        BendSuggestionScanWorkflowRequest request,
        BendSuggestionScanWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (actions.IsBusy())
            return new BendSuggestionScanWorkflowResult(false, false, null, "Ein Durchlauf laeuft bereits.");

        var cancellationToken = actions.ResetCancellation();
        try
        {
            actions.SetBusy(true);
            actions.SetStatusText(
                $"Bogen-Vorschlaege {request.Haltung}: Bilder werden extrahiert und geprueft ...");

            var progress = new Progress<BendSuggestionScanProgress>(wert =>
            {
                if (wert.Processed % 25 != 0 && wert.Processed != wert.Total)
                    return;
                actions.SetStatusText(
                    $"Bogen-Vorschlaege {request.Haltung}: Bild {wert.Processed} von {wert.Total} ...");
            });

            var result = await actions.Scan(
                request.Scan, cancellationToken, progress).ConfigureAwait(false);

            if (!result.IsUsable)
            {
                // Die Meldung kommt bereits verstaendlich aus der Kalibrierung;
                // sie wird woertlich weitergereicht. Keine Liste ohne Arbeitspunkt.
                actions.SetStatusText($"Bogen-Vorschlaege {request.Haltung}: {result.Reason}");
                actions.Log($"Bogen-Vorschlaege {request.Haltung}: {result.Reason}");
                return new BendSuggestionScanWorkflowResult(true, false, result, result.Reason);
            }

            var nichtAusgewertet = result.FramesNotAssessed > 0
                ? $", {result.FramesNotAssessed} Bilder nicht ausgewertet"
                : string.Empty;
            var abschluss =
                $"Bogen-Vorschlaege {request.Haltung}: {result.Suggestions.Count} Stellen in "
                + $"{result.Duration.TotalSeconds:0} s ({result.FramesAnalyzed} Bilder{nichtAusgewertet}).";
            actions.SetStatusText(abschluss);
            actions.Log(abschluss);
            actions.PublishResult(result, request.Haltung);
            return new BendSuggestionScanWorkflowResult(true, true, result, null);
        }
        catch (OperationCanceledException)
        {
            var meldung = $"Bogen-Vorschlaege {request.Haltung}: abgebrochen.";
            actions.SetStatusText(meldung);
            actions.Log(meldung);
            return new BendSuggestionScanWorkflowResult(true, false, null, "abgebrochen");
        }
        catch (Exception ex)
        {
            var meldung = $"Bogen-Vorschlaege {request.Haltung} fehlgeschlagen: {ex.Message}";
            actions.SetStatusText(meldung);
            actions.Log(meldung);
            return new BendSuggestionScanWorkflowResult(true, false, null, ex.Message);
        }
        finally
        {
            actions.SetBusy(false);
        }
    }
}
