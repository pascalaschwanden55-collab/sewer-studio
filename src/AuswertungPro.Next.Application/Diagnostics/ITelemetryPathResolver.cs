namespace AuswertungPro.Next.Application.Diagnostics;

/// <summary>Bestimmt den Speicherort einer Telemetriedatei.</summary>
public interface ITelemetryPathResolver
{
    string? ResolveFile(string fileName);
}
