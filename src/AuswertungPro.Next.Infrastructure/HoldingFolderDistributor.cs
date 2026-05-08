using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure;

/// <summary>
/// Hoch-Level-Orchestrator fuer das Verteilen von Inspektions-Sidecar-Dateien
/// (PDF, TXT, Video, XTF) in Haltungs-Ordner.
///
/// Audit-Fix 2026-04: Die Klasse war 4616 Zeilen mit 24 public + 146 private Methoden.
/// Ueber 'partial class' jetzt physisch auf mehrere Dateien verteilt:
///   - HoldingFolderDistributor.cs              -> Public API + Distribute/DistributeCore
///   - HoldingFolderDistributor.DateParsing.cs  -> Date-Regex + Parse-Helpers
///
/// Verhalten unveraendert. Tests laufen ohne Anpassung.
/// </summary>
public static partial class HoldingFolderDistributor
{
    // SchachtDateIndexSync + SchachtDateIndexCache ausgegliedert nach
    // HoldingFolderDistributor.SchachtPdfParsing.cs
    // (Refactor 2026-05-07, Charge R7).

    // Regex-Patterns ausgegliedert nach HoldingFolderDistributor.Regex.cs
    // (Refactor 2026-05-07, Charge R1).

    private static readonly object XtfCacheSync = new();
    private static readonly Dictionary<string, string[]> XtfFilesCache =
        new(StringComparer.OrdinalIgnoreCase);
    // Public types ausgegliedert nach HoldingFolderDistributor.Types.cs
    // (Refactor 2026-05-07, Charge R2).

    private sealed record KinsTxtSection(
        string SourceTxtPath,
        string HoldingRaw,
        string VideoFileName,
        DateTime Date,
        string SectionText);


    // Distribute, DistributeFiles, DistributeTxt, DistributeTxtFiles,
    // DistributeTxtCore, DistributeCore ausgegliedert nach
    // HoldingFolderDistributor.Distribute.cs
    // (Refactor 2026-05-07, Charge R9).



    // I/O-Helpers (CopyCandidatesToUnmatched, BuildMissingInfo,
    // BuildAmbiguousInfo, MoveOrCopy) ausgegliedert nach
    // HoldingFolderDistributor.IO.cs (Refactor 2026-05-07, Charge R3).

    // TXT-Parsing-Methoden wurden 2026-05-07 in HoldingFolderDistributor.TxtParsing.cs ausgelagert.

    // EnumerateVideoFiles, EnumerateSidecarFiles, BuildSidecarVideoLinkIndex,
    // BuildSidecarHoldingByVideoIndex, EnumerateVideoLookupKeys,
    // BuildCdIndexVideoLinkIndex, ResolveCdIndexFolders, AddCdIndexMappings,
    // ResolveSidecarFolders, FindVideo, FindVideoByHaltungDate,
    // TryFindVideoFromSidecarLinks, TryFindVideoFromCdIndexPhotoHints,
    // TryResolveHoldingFromMatchedVideo, HoldingHasVideoLink,
    // GetSuffixFromFirstUnderscore ausgegliedert nach
    // HoldingFolderDistributor.VideoMatching.cs
    // (Refactor 2026-05-07, Charge R8).

    // PageInfo, PdfPageChunk, ReadPdfPages, ReadPdfPagesWithPdfPig,
    // ReadPdfText, NormalizeText ausgegliedert nach
    // HoldingFolderDistributor.PdfReading.cs
    // (Refactor 2026-05-08, Charge R13).

    // ParsedPdf / ParsedShaftPdf ausgegliedert nach HoldingFolderDistributor.Types.cs
    // (Refactor 2026-05-07, Charge R2).

    // TryParseDateString ausgegliedert nach HoldingFolderDistributor.DateParsing.cs
    // (Refactor 2026-05-07, Charge R5).


    // ParseSchachtPdf + ParseSchachtPdfPage ausgegliedert nach
    // HoldingFolderDistributor.SchachtPdfParsing.cs
    // (Refactor 2026-05-07, Charge R7).


    // DistributeShafts, DistributeShaftFiles, DistributeShaftCore,
    // ExpandSelectedShaftPdfFiles ausgegliedert nach
    // HoldingFolderDistributor.DistributeShafts.cs
    // (Refactor 2026-05-07, Charge R10).

    // --- Dichtheitspruefungsprotokoll Distribution ---

    // DistributeDichtheit, DistributeDichtheitFiles, DistributeDichtheitCore,
    // ExtractDichtheitPerPage, TryExtractDichtheitShafts,
    // ResolveDichtheitHaltungOrder ausgegliedert nach
    // HoldingFolderDistributor.DistributeDichtheit.cs
    // (Refactor 2026-05-07, Charge R11).



    // ParseSchachtPdfPageWithOcrFallback, TryParseSchachtPdfPageFromFormFields,
    // BuildSyntheticFormText, TryExtractSchachtNumberFromFormEntries,
    // BuildFormEntryLabel, ContainsDateLabel, ContainsSchachtNumberLabel,
    // ExtractShaftNumberToken, TryCompleteShaftDateFromSiblingProtocol,
    // TryResolveDateFromSiblingProtocol, GetOrBuildSchachtDateIndex,
    // BuildSchachtDateIndex, NormalizeShaftNumberKey
    // ausgegliedert nach HoldingFolderDistributor.SchachtPdfParsing.cs
    // (Refactor 2026-05-07, Charge R7).

    // BuildPageRange + IsContentsPage ausgegliedert nach
    // HoldingFolderDistributor.PdfReading.cs
    // (Refactor 2026-05-08, Charge R13).

    // TryFindInspectionDate, TryFindSchachtDate, FindNearbyDate
    // ausgegliedert nach HoldingFolderDistributor.DateParsing.cs
    // (Refactor 2026-05-07, Charge R5).

    // TryFindHaltungId, TryParseKsCompactHoldingDigits, TryFindSchachtNumber,
    // WinCanValueRegex, WinCanUpperLabelRegex, WinCanLowerLabelRegex,
    // NormalizeLine, TryGetValueAfterLabel, TryExtractFromHeader,
    // LooksLikeDateFragment, TryExtractFromShafts, TryFindPoint,
    // FindNextToken ausgegliedert nach
    // HoldingFolderDistributor.HaltungExtraction.cs
    // (Refactor 2026-05-08, Charge R14).











    // HandleParsedDistribution, FindRecordByHolding, TryFindVideoFromRecordLink,
    // HandleParsedShaftDistribution, TryMatchPdfToHolding ausgegliedert nach
    // HoldingFolderDistributor.Pipeline.cs
    // (Refactor 2026-05-08, Charge R15).


    // NodePrefixRegex, StripNodePrefixes, EnumerateHoldingLookupKeys,
    // ReverseHoldingId ausgegliedert nach HoldingFolderDistributor.Util.cs
    // (Refactor 2026-05-07, Charge R4).




    // Photo-Hint-Extraction (PhotoAfterLabelRegex, PhotoTokenRegex,
    // ExtractPhotoHintsFromPdf, AddPhotoLookupKeys, EnumeratePhotoLookupKeys)
    // ausgegliedert nach HoldingFolderDistributor.PhotoHints.cs
    // (Refactor 2026-05-07, Charge R12).


    // String-/ID-Normalisierungs-Helfer (NormalizePhotoToken, Tokenize,
    // HasVideoExtension, HasImageExtension, NormalizeVideoFileName,
    // SanitizePathSegment, HoldingFromKiasFilename, NormalizeHaltungId,
    // NormalizeKey, IsValidHaltungId) ausgegliedert nach
    // HoldingFolderDistributor.Util.cs (Refactor 2026-05-07, Charge R4).

    /// <summary>
    /// Prueft ob im Haltungsordner bereits ein Video mit gleicher Dateigroesse existiert.
    /// Gibt den Pfad zurueck wenn ja, sonst null.
    /// Verhindert Duplikate beim erneuten Verteilen.
    /// </summary>
    // FindExistingVideo ausgegliedert nach HoldingFolderDistributor.IO.cs
    // (Refactor 2026-05-07, Charge R3).

    /// <summary>
    /// Versucht, ein nicht-parsbares PDF (z.B. Dichtheitspruefungsprotokoll) anhand
    /// seines Dateinamens einem bereits verteilten Haltungsordner zuzuordnen.
    /// Sucht nach Haltungsnummern im Dateinamen und vergleicht mit dem Index.
    /// </summary>

    // EnsureUniquePath ausgegliedert nach HoldingFolderDistributor.IO.cs
    // (Refactor 2026-05-07, Charge R3).

}
