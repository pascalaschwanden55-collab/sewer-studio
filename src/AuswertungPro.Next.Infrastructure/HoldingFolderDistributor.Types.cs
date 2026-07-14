using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Infrastructure;

// Datentypen (records/enum) des HoldingFolderDistributor.
// Teil derselben partial-Klasse — nur in eine eigene Datei ausgelagert (kein Verhaltenswechsel).
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
        Ambiguous
    }

    public sealed record VideoFindResult(
        VideoMatchStatus Status,
        string? VideoPath,
        IReadOnlyList<string> Candidates,
        string? Message);

    private sealed record KinsTxtSection(
        string SourceTxtPath,
        string HoldingRaw,
        string VideoFileName,
        DateTime Date,
        string SectionText);

}
