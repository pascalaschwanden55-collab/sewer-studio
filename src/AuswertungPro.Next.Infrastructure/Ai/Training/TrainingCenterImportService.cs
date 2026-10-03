using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

public sealed class TrainingCenterImportService
{
    // Paket A (03.10.2026): Die Fassade behaelt alle oeffentlichen und internen Signaturen und delegiert an
    // TrainingCenterFallScan, TrainingCenterHaltungsverteilung, TrainingCenterPaarung, TrainingCenterVideoIndex
    // und TrainingCenterProtokollJson; die Pruefregeln der Fallordner-Dateien stehen in TrainingCenterFallDateien.
    private readonly TrainingCenterFallScan _scan;
    private readonly TrainingCenterHaltungsverteilung _verteilung;

    public TrainingCenterImportService()
        : this(null, null, null)
    {
    }

    /// <summary>
    /// Testnaht: PDF-Leser, Dateiliste je Ordner, ein Haken nach jedem fertig angelegten
    /// Haltungsordner der Verteilung und der Attributleser der Verweispruefung (wie
    /// <see cref="VerknuepfungsSchutz.PruefeEintrag"/>). Ohne Angabe gelten die echten Dateizugriffe.
    /// </summary>
    internal TrainingCenterImportService(
        Func<string, PdfTextExtraction>? pdfSeitenLesen,
        Func<string, IEnumerable<string>>? dateienImOrdner,
        Action<string>? nachHaltungsordner,
        Func<string, FileAttributes?>? leseAttribute = null)
    {
        var pdfLeser = pdfSeitenLesen ?? (pfad => PdfTextExtractor.ExtractPages(pfad));
        var dateiliste = dateienImOrdner
                         ?? (ordner => Directory.EnumerateFiles(ordner, "*.*", SearchOption.TopDirectoryOnly));
        var fallDateien = new TrainingCenterFallDateien(TrainingCenterFallScan.VideoExts, leseAttribute);
        _scan = new TrainingCenterFallScan(dateiliste, fallDateien);
        _verteilung = new TrainingCenterHaltungsverteilung(pdfLeser, dateiliste, nachHaltungsordner, leseAttribute, fallDateien);
    }

    public Task<List<TrainingCaseInput>> ScanAsync(string rootFolder)
        => ScanAsync(rootFolder, null, CancellationToken.None);

    /// <summary>
    /// Sucht Trainingsfaelle unter <paramref name="rootFolder"/>. Laeuft ausserhalb des
    /// aufrufenden Threads (Deepscan R6: das Training Center fror beim Scan grosser Ablagen ein);
    /// der Abbruch wird vor jedem Ordner geprueft. Nicht lesbare oder verknuepfte Ordner landen
    /// in <paramref name="uebersprungeneOrdner"/> statt still zu fehlen (Deepscan R8).
    /// </summary>
    public Task<List<TrainingCaseInput>> ScanAsync(
        string rootFolder,
        ICollection<string>? uebersprungeneOrdner,
        CancellationToken cancellationToken)
        => ScanAsync(rootFolder, uebersprungeneOrdner, null, cancellationToken);

    /// <summary>
    /// Wie oben; zusaetzlich landen Hinweise zu ungueltigen oder unlesbaren Videoverweisen
    /// (<c>&lt;Video&gt;.link</c>) in <paramref name="hinweise"/> (PR #85).
    /// </summary>
    public Task<List<TrainingCaseInput>> ScanAsync(
        string rootFolder,
        ICollection<string>? uebersprungeneOrdner,
        ICollection<string>? hinweise,
        CancellationToken cancellationToken)
        => Task.Run(() => _scan.Scan(rootFolder, uebersprungeneOrdner, hinweise, cancellationToken), cancellationToken);

    /// <summary>
    /// Scannt nur nach Protokollen (PDF/JSON), Video ist nicht erforderlich.
    /// Fuer den reinen Protokoll-Import ohne Videoanalyse.
    /// </summary>
    public Task<List<TrainingCaseInput>> ScanProtocolOnlyAsync(string rootFolder)
        => Task.FromResult(_scan.ScanProtocolOnly(rootFolder));

    internal static (string VideoPath, string ProtocolPath) ResolvePair(
        IReadOnlyList<string> videos,
        IReadOnlyList<string> protos,
        string caseId)
        => TrainingCenterPaarung.ResolvePair(videos, protos, caseId);

    /// <summary>
    /// Wie oben; <paramref name="mehrdeutigeVideos"/> sagt, ob mehrere echte (nicht ausgeschlossene) Videos ohne
    /// eindeutigen Haltungsschluessel vorlagen und deshalb keines gewaehlt wurde (Folgepaket 3).
    /// </summary>
    internal static (string VideoPath, string ProtocolPath) ResolvePair(
        IReadOnlyList<string> videos,
        IReadOnlyList<string> protos,
        string caseId,
        out bool mehrdeutigeVideos)
        => TrainingCenterPaarung.ResolvePair(videos, protos, caseId, out mehrdeutigeVideos);

    internal static (string VideoPath, string ProtocolPath) ResolveProtocolOnlyPair(
        IReadOnlyList<string> videos,
        IReadOnlyList<string> protos,
        string caseId)
        => TrainingCenterPaarung.ResolveProtocolOnlyPair(videos, protos, caseId);

    // ── Haltungs-Verteilung ─────────────────────────────────────────────────
    // Teilt ein Multi-Haltungs-PDF in einzelne Ordner auf und ordnet Videos zu.

    public sealed record DistributeResult(
        int TotalChunks,
        int Distributed,
        int VideosMatched,
        int Uncertain,
        string OutputFolder,
        List<string> Messages);

    /// <summary>
    /// Verteilt ein Multi-Haltungs-PDF + Video-Ordner in einzelne Unterordner.
    /// Pro Haltung wird ein Ordner mit JSON-Protokoll und Video-Verweis erstellt.
    /// Laeuft ausserhalb des aufrufenden Threads (Deepscan R6); der Abbruch wird vor dem
    /// PDF-Lesen und vor jeder Haltung geprueft und wirft <see cref="OperationCanceledException"/>.
    /// </summary>
    /// <summary>
    /// Bisherige dreistellige Signatur, binaer kompatibel fuer bereits kompilierte Aufrufer (Review PR #85).
    /// </summary>
    public Task<DistributeResult> DistributeByHaltungAsync(string pdfPath, string videoFolder, string outputFolder)
        => DistributeByHaltungAsync(pdfPath, videoFolder, outputFolder, CancellationToken.None);

    public Task<DistributeResult> DistributeByHaltungAsync(
        string pdfPath, string videoFolder, string outputFolder, CancellationToken cancellationToken)
        => Task.Run(
            () => _verteilung.Verteile(pdfPath, videoFolder, outputFolder, new List<string>(), cancellationToken),
            cancellationToken);

    /// <summary>
    /// Wie oben; zusaetzlich landen alle Meldungen in <paramref name="meldungen"/> – auch bei Abbruch oder Fehler,
    /// damit die Meldungen bereits verarbeiteter Haltungen nicht verloren gehen (Folgepaket 2). Der Sammler wird
    /// erst nach dem Ende der Hintergrundarbeit befuellt (kein gleichzeitiger Zugriff).
    /// </summary>
    public async Task<DistributeResult> DistributeByHaltungAsync(
        string pdfPath,
        string videoFolder,
        string outputFolder,
        ICollection<string> meldungen,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(meldungen);
        var messages = new List<string>();
        try
        {
            return await Task.Run(
                () => _verteilung.Verteile(pdfPath, videoFolder, outputFolder, messages, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            foreach (var meldung in messages)
                meldungen.Add(meldung);
        }
    }

    /// <summary>
    /// Erstellt einen Index: normalisierte Haltungs-ID → Videodatei-Pfad
    /// </summary>
    internal static Dictionary<string, string> BuildVideoIndex(
        string videoFolder,
        List<string> messages,
        CancellationToken cancellationToken)
        => TrainingCenterVideoIndex.BuildVideoIndex(videoFolder, messages, cancellationToken);

    /// <summary>
    /// Extrahiert Beobachtungen aus dem Chunk-Text (Fretz-Format + Standard).
    /// Unbekannte Codes werden am Parse-Eintritt verworfen, damit PDF-Freitext
    /// nicht als Trainingslabel in den Batch gelangt.
    /// </summary>
    internal static List<ProtocolEntry> ExtractEntriesFromChunkText(string text)
        => TrainingCenterProtokollJson.ExtractEntriesFromChunkText(text);

    internal sealed record ProtocolEntry(string Code, string Beschreibung, double MeterStart);
}
