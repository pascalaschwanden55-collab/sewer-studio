namespace AuswertungPro.Next.Application.Diagnostics;

public sealed class DiagnosticsOptions
{
    public bool EnableDiagnostics { get; set; } = true;
    public string? ExplicitPdfToTextPath { get; set; }
}
