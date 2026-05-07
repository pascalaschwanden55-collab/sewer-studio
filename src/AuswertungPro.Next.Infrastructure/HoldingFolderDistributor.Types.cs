using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Infrastructure;

/// <summary>
/// Public Types (Records + Enum) fuer HoldingFolderDistributor.
///
/// Refactor 2026-05-07 (Etappe 1, Charge R2): mechanisch ausgegliedert
/// aus der Hauptdatei. Keine Verhaltensaenderung — die Records waren
/// schon vorher als nested types der partial class deklariert, jetzt
/// physisch in eigene Datei verschoben.
/// </summary>
public static partial class HoldingFolderDistributor
{
    public sealed record DistributionResult(
        bool Success,
        string Message,
        string SourcePdfPath,
        string? SourceVideoPath,
        string? DestPdfPath,
        string? DestVideoPath,
        string? InfoPath,
        string? HoldingFolder,
        VideoMatchStatus VideoStatus,
        bool PdfCorrected = false,
        string? PdfCorrectionMessage = null);

    public sealed record DistributionProgress(int Processed, int Total, string? CurrentFile);

    public enum VideoMatchStatus
    {
        NotChecked,
        Matched,
        NotFound,
        Ambiguous,
        /// <summary>Match nur ueber Haltungsname ohne Datum-Verifikation.
        /// Funktional eine Zuordnung, aber bei mehreren Projekten / Wiederholungsinspektionen
        /// kann das falsch sein. UI/Devis sollten zur manuellen Pruefung markieren.</summary>
        MatchedWithoutDate,
    }

    public sealed record VideoFindResult(
        VideoMatchStatus Status,
        string? VideoPath,
        IReadOnlyList<string> Candidates,
        string? Message);

    // Temporarily public for diagnostic purposes
    public sealed record ParsedPdf(bool Success, string? Message, DateTime? Date, string? Haltung, string? VideoFile);
    public sealed record ParsedShaftPdf(bool Success, string? Message, DateTime? Date, string? ShaftNumber);
}
