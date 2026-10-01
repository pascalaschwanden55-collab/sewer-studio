using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Dossiers;

namespace AuswertungPro.Next.UI.Views.Windows;

internal sealed record DossierPlanWidthInputResult(
    bool Success,
    double? WidthCm,
    string? Error);

internal static class DossierPlanWidthInputParser
{
    internal static DossierPlanWidthInputResult Parse(string? raw)
    {
        var text = raw?.Trim() ?? string.Empty;
        if (text.Length == 0)
            return new DossierPlanWidthInputResult(true, null, null);

        if (!FachzahlParser.TryParseMeasurement(text, out var parsed))
        {
            return new DossierPlanWidthInputResult(
                false,
                null,
                "Die Breite ist keine eindeutige Zahl.");
        }

        var widthCm = (double)parsed;
        if (widthCm is <= 0 or > DossierWordTemplateExportService.PlanMaxWidthCm)
        {
            return new DossierPlanWidthInputResult(
                false,
                null,
                "Die Breite muss zwischen 1 und 15 cm liegen.");
        }

        return new DossierPlanWidthInputResult(true, widthCm, null);
    }
}
