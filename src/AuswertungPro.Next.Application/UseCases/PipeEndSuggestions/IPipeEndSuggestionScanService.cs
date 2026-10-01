using System;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.UseCases.PipeEndSuggestions;

/// <summary>Fortschritt des Durchlaufs: verarbeitete Bilder von insgesamt, je Klasse.</summary>
public sealed record PipeEndScanProgress(PipeEndKind Kind, int Processed, int Total);

/// <summary>
/// Vorabdurchlauf eines Videos fuer Rohranfang und Rohrende: liefert je Klasse
/// hoechstens eine Stelle zum menschlichen Bestaetigen oder Korrigieren.
///
/// Wie beim Bogen ein Vorablauf und keine Live-Einblendung im Player. Die
/// Freigabe (2026-08-12) wurde genau fuer diese Regel gemessen: Rohranfang
/// Precision 85 % / Recall 98 %, Rohrende 89 % / 88 %, an Clips beurteilt.
/// </summary>
public interface IPipeEndSuggestionScanService
{
    Task<PipeEndScanResult> ScanAsync(
        PipeEndScanRequest request,
        CancellationToken cancellationToken,
        IProgress<PipeEndScanProgress>? progress = null);
}
