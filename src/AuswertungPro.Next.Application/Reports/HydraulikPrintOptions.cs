namespace AuswertungPro.Next.Application.Reports;

public sealed record HydraulikPrintOptions
{
    public bool IncludeTeilfuellung { get; init; } = true;
    public bool IncludeVollfuellung { get; init; } = true;
    public bool IncludeKennzahlen { get; init; } = true;
    public bool IncludeAblagerung { get; init; } = true;
    public bool IncludeAuslastung { get; init; } = true;
    public bool IncludeBewertung { get; init; } = true;
    public string? LogoPathAbs { get; init; }
    public string FooterLine { get; init; } = "";
}
