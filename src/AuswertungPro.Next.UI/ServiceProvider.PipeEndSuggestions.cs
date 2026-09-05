using System;
using System.IO;
using AuswertungPro.Next.Application.UseCases.PipeEndSuggestions;
using AuswertungPro.Next.Infrastructure.Ai.PipeEndSuggestions;
using AuswertungPro.Next.Infrastructure.Media;

namespace AuswertungPro.Next.UI;

public sealed partial class ServiceProvider
{
    private IPipeEndSuggestionScanService? _pipeEndSuggestionScan;

    /// <summary>
    /// Rohranfang/Rohrende im Vorabdurchlauf (Lernstufen-Freigabe 2026-08-12).
    /// Derselbe Sidecar-Client wie der Bogen-Copilot; der Sidecar fuehrt nur
    /// freigegebene Gewichte, C# pinnt Klasse und Hash je Anfrage. Wird beim
    /// ersten Zugriff gebaut — die Hauptdatei des ServiceProviders bleibt so
    /// unter der 1000-Zeilen-Grenze des Wartbarkeitswaechters.
    /// </summary>
    public IPipeEndSuggestionScanService PipeEndSuggestionScan
        => _pipeEndSuggestionScan ??= new PipeEndSuggestionScanService(
            new VideoFrameSequenceExtractor(),
            (anfrage, abbruch) => _bendSuggestionClient.Value.ClassifyLernstufeAsync(anfrage, abbruch),
            FfmpegExecutables.ResolveFfmpeg,
            () => Path.Combine(Path.GetTempPath(), "auswertungpro-anfang-ende-scan"));
}
