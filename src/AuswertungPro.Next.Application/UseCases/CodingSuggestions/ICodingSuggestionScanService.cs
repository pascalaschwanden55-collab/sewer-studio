using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using AuswertungPro.Next.Application.UseCases.PipeEndSuggestions;

namespace AuswertungPro.Next.Application.UseCases.CodingSuggestions;

/// <summary>Vorabdurchlauf fuer den Codiermodus; der Player kennt nur diesen Vertrag.</summary>
public interface ICodingSuggestionScanService
{
    Task<CodingSuggestionSet> ScanAsync(
        CodingSuggestionScanRequest request,
        CancellationToken cancellationToken,
        IProgress<int>? percent = null);
}

/// <summary>
/// Verdrahtet den UseCase mit den zwei bestehenden Durchlaeufen und dem
/// Sitzungsgedaechtnis. Enthaelt selbst keine Regel.
/// </summary>
public sealed class CodingSuggestionScanService : ICodingSuggestionScanService
{
    private readonly IBendSuggestionScanService _bends;
    private readonly IPipeEndSuggestionScanService _pipeEnds;
    private readonly ICodingSuggestionExposure _exposure;

    public CodingSuggestionScanService(
        IBendSuggestionScanService bends,
        IPipeEndSuggestionScanService pipeEnds,
        ICodingSuggestionExposure exposure)
    {
        _bends = bends ?? throw new ArgumentNullException(nameof(bends));
        _pipeEnds = pipeEnds ?? throw new ArgumentNullException(nameof(pipeEnds));
        _exposure = exposure ?? throw new ArgumentNullException(nameof(exposure));
    }

    public Task<CodingSuggestionSet> ScanAsync(
        CodingSuggestionScanRequest request,
        CancellationToken cancellationToken,
        IProgress<int>? percent = null)
    {
        var bogenFortschritt = percent is null
            ? null
            : new Progress<BendSuggestionScanProgress>(p =>
                percent.Report(CodingSuggestionScanUseCase.Percent(true, p.Processed, p.Total)));
        var endenFortschritt = percent is null
            ? null
            : new Progress<PipeEndScanProgress>(p =>
                percent.Report(CodingSuggestionScanUseCase.Percent(false, p.Processed, p.Total)));

        return CodingSuggestionScanUseCase.ExecuteAsync(
            request,
            new CodingSuggestionScanActions(
                ScanBends: (r, ct) => _bends.ScanAsync(r, ct, bogenFortschritt),
                ScanPipeEnds: (r, ct) => _pipeEnds.ScanAsync(r, ct, endenFortschritt),
                MarkExposed: _exposure.MarkExposed)
            {
                ReportPercent = percent is null ? null : percent.Report
            },
            cancellationToken);
    }
}
