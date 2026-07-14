namespace AuswertungPro.Next.Application.Reports;

public sealed record ProtocolPdfExportOptions
{
    public bool ShowAiHints { get; init; }
    public bool ShowAiHintsOnlyIfDecided { get; init; } = true;
    public bool ShowAiSummary { get; init; }
    public int MaxAiSummaryCodes { get; init; } = 5;
    public char CsvDelimiter { get; init; } = ';';
    public bool CsvIncludeBom { get; init; } = true;
    public bool CsvIncludeAiColumns { get; init; } = true;
}

public sealed record HaltungsprotokollPdfOptions
{
    public string Title { get; init; } = "Haltungsinspektion";
    public string Subtitle { get; init; } = "SN EN 13508-2";
    public string SenderBlock { get; init; } =
        "Abwasser Uri\n" +
        "Zentrale Dienste\n" +
        "Giessenstrasse 46\n" +
        "6460 Altdorf\n" +
        "info@abwasser-uri.ch\n" +
        "T 041 875 00 90";

    public bool IncludePhotos { get; init; } = true;
    public bool IncludeHaltungsgrafik { get; init; } = true;

    /// <summary>Detaillierte Beobachtungstabelle unter der Haltungsgrafik anzeigen.</summary>
    public bool IncludeObservationTable { get; init; } = true;

    /// <summary>Optionaler Code-Katalog fuer lesbare Quantifizierungen.</summary>
    public Protocol.ICodeCatalogProvider? CodeCatalog { get; init; }
    public int PhotosPerRow { get; init; } = 1;
    public int PhotosPerPage { get; init; } = 2;
    public int MaxPhotosPerEntry { get; init; } = int.MaxValue;
    public float PhotoWidth { get; init; } = 500f;
    public float PhotoHeight { get; init; } = 255f;
    public float PhotoSpacing { get; init; } = 12f;
    public string? LogoPathAbs { get; init; }
    public string FooterLine { get; init; } = "";
    public AiOptimizationResult? AiOptimization { get; init; }
}

/// <summary>Abgeflachter Stand einer KI-Sanierungsoptimierung fuer den PDF-Export.</summary>
public sealed record AiOptimizationResult
{
    public string RecommendedMeasure { get; init; } = "";
    public string CostBandText { get; init; } = "";
    public double Confidence { get; init; }
    public string Reasoning { get; init; } = "";
    public string RiskText { get; init; } = "";
    public bool IsFallback { get; init; }
}
