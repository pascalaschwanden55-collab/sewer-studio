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
    public string? LogoPathAbs { get; init; }
    public string FooterLine { get; init; } = "";

    /// <summary>Kostendaten fuer die Haltung (aus ProjectCostStore).</summary>
    public HoldingCost? HoldingCost { get; init; }

    /// <summary>Absolute Pfade zu Original-PDF-Protokollen (aufgeloest).</summary>
    public IReadOnlyList<string>? OriginalPdfPaths { get; init; }

    /// <summary>Optionale historische Vergleichsreferenz (aus HistorischeSanierungenService).
    /// Wird im Devis-PDF als Plausibilitaets-Check gegen die berechneten Kosten angezeigt.</summary>
    public HistorischeReferenz? HistorischeReferenz { get; init; }
}

/// <summary>Vergleichsdaten aus historischen Sanierungen (Bürglen 2024-2026).</summary>
public sealed record HistorischeReferenz
{
    public string ProfilLabel { get; init; } = "";       // z.B. "DN300, NBR, Mischabwasser"
    public int AnzahlFaelle { get; init; }
    public decimal? KostenProMMedianChf { get; init; }
    public decimal? KostenProMMinChf { get; init; }
    public decimal? KostenProMMaxChf { get; init; }
    public decimal? KostenProHaltungMedianChf { get; init; }
    public IReadOnlyList<string> TypischeMassnahmen { get; init; } = Array.Empty<string>();
    public string Quelle { get; init; } = "Auswertungen Bürglen 2024-2026";
}
