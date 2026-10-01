using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using AuswertungPro.Next.Application.UseCases.PipeEndSuggestions;

namespace AuswertungPro.Next.Application.UseCases.CodingSuggestions;

/// <param name="Haltung">Sichtbarer Haltungsname; Schluessel des Sitzungsgedaechtnisses.</param>
/// <param name="Enabled">Schalter aus den Einstellungen; false startet nichts.</param>
public sealed record CodingSuggestionScanRequest(string VideoPath, string Haltung, bool Enabled);

/// <summary>Aussenverbindungen — eingehaengt, damit die Regeln ohne Sidecar pruefbar sind.</summary>
public sealed record CodingSuggestionScanActions(
    Func<BendSuggestionScanRequest, CancellationToken, Task<BendSuggestionScanResult>> ScanBends,
    Func<PipeEndScanRequest, CancellationToken, Task<PipeEndScanResult>> ScanPipeEnds,
    Action<string> MarkExposed)
{
    /// <summary>Gesamtfortschritt 0..100; optional, rein nach aussen.</summary>
    public Action<int>? ReportPercent { get; init; }
}

/// <summary>
/// Vorabdurchlauf fuer den Codiermodus: zuerst Bogen, dann Rohranfang/Rohrende
/// (alle drei Gewichte teilen den Slot YOLO_TEST). Jeder Teil faellt fuer sich
/// aus; ein technischer Fehler ist nie "kein Vorschlag"; ein Abbruch geht durch.
/// </summary>
public static class CodingSuggestionScanUseCase
{
    public static async Task<CodingSuggestionSet> ExecuteAsync(
        CodingSuggestionScanRequest request,
        CodingSuggestionScanActions actions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.Enabled)
            return CodingSuggestionSet.Leer("In den Einstellungen ausgeschaltet.");

        var vorschlaege = new List<CodingSuggestion>();
        IReadOnlyList<MeterTrackPoint> spur = Array.Empty<MeterTrackPoint>();

        // --- Teil 1: Bogen ---
        CodingSuggestionPartState bogenTeil;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bogen = await actions.ScanBends(
                new BendSuggestionScanRequest
                {
                    VideoPath = request.VideoPath,
                    CandidateId = CodingBendCandidatePin.Id,
                    WeightSha256 = CodingBendCandidatePin.WeightSha256
                },
                cancellationToken).ConfigureAwait(false);

            if (bogen.IsUsable)
            {
                bogenTeil = CodingSuggestionPartState.Bereit;
                spur = bogen.MeterTrack;
                vorschlaege.AddRange(bogen.Suggestions.Select(s => new CodingSuggestion(
                    CodingSuggestionKind.Bogen,
                    s.PeakTimeSeconds,
                    s.MeterStart,
                    s.MeterIsEstimated,
                    s.MaxConfidence,
                    s.Strength == BendSuggestionStrength.Strong,
                    AcceptancePrecision: 0.0)));
            }
            else
            {
                bogenTeil = CodingSuggestionPartState.NichtVerfuegbar(bogen.Reason);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Ein technischer Fehler ist ein blinder Fleck, kein "kein Bogen".
            bogenTeil = CodingSuggestionPartState.Fehler(ex.Message);
        }

        actions.ReportPercent?.Invoke(50);

        // --- Teil 2: Rohranfang / Rohrende ---
        CodingSuggestionPartState endenTeil;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var enden = await actions.ScanPipeEnds(
                new PipeEndScanRequest { VideoPath = request.VideoPath },
                cancellationToken).ConfigureAwait(false);

            endenTeil = CodingSuggestionPartState.Bereit;
            vorschlaege.AddRange(enden.Suggestions.Select(s => new CodingSuggestion(
                s.Kind == PipeEndKind.Rohranfang ? CodingSuggestionKind.Rohranfang : CodingSuggestionKind.Rohrende,
                s.PeakTimeSeconds,
                Meter: null,
                MeterIsEstimated: false,
                s.MaxConfidence,
                IsStrong: true,
                AcceptancePrecision: Pin(s.Kind).Precision)));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            endenTeil = CodingSuggestionPartState.Fehler(ex.Message);
        }

        actions.ReportPercent?.Invoke(100);

        var sortiert = vorschlaege.OrderBy(v => v.PeakTimeSeconds).ToList();
        if (sortiert.Count > 0)
            actions.MarkExposed(request.Haltung);

        return new CodingSuggestionSet(sortiert, spur, bogenTeil, endenTeil);
    }

    /// <summary>Bogen belegt 0..50 %, Anfang/Ende 50..100 %.</summary>
    public static int Percent(bool bogenPhase, int processed, int total)
    {
        if (total <= 0)
            return bogenPhase ? 0 : 50;
        var anteil = Math.Clamp(processed / (double)total, 0.0, 1.0);
        var basis = bogenPhase ? 0.0 : 50.0;
        return (int)Math.Round(basis + anteil * 50.0);
    }

    private static PipeEndLernstufePin Pin(PipeEndKind kind)
        => kind == PipeEndKind.Rohranfang ? PipeEndLernstufePins.Rohranfang : PipeEndLernstufePins.Rohrende;
}
