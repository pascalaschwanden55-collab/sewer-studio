using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Costs;

/// <summary>Erzeugt das NPK-Leistungsverzeichnis als Excel-Datei im Speicher.</summary>
public interface INpkLeistungsverzeichnisExcelExporter
{
    byte[] BuildWorkbook(
        IReadOnlyList<AggregatedPosition> positions,
        string currency = "CHF",
        decimal vatRate = 0.081m,
        string projectName = "",
        decimal excludedPauschaleTotal = 0m,
        int excludedPauschaleCount = 0,
        string? logoPathAbs = null);
}
