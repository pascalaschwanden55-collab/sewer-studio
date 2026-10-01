using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Reports;

public sealed record DossierPrintOptions
{
    public bool IncludeDeckblatt { get; init; } = true;
    public bool IncludeHaltungsprotokoll { get; init; } = true;
    public bool IncludeFotos { get; init; } = true;
    public bool IncludeSchachtVon { get; init; } = true;
    public bool IncludeSchachtBis { get; init; } = true;
    public bool IncludeHydraulik { get; init; } = true;
    public bool IncludeKostenschaetzung { get; init; } = true;
    public bool IncludeOriginalProtokolle { get; init; } = true;
    /// <summary>
    /// Anzahl Fotos je Fotoseite (1, 2, 4 oder 6). <c>null</c> bedeutet: die Einstellung
    /// des Benutzers verwenden.
    /// </summary>
    public int? PhotosPerPage { get; init; }

    public string? LogoPathAbs { get; init; }
    public string FooterLine { get; init; } = "";

    /// <summary>Kostendaten fuer die Haltung (aus ProjectCostStore).</summary>
    public HoldingCost? HoldingCost { get; init; }

    /// <summary>Absolute Pfade zu Original-PDF-Protokollen (aufgeloest).</summary>
    public IReadOnlyList<string>? OriginalPdfPaths { get; init; }
}
