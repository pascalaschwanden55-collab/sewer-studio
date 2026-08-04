using AuswertungPro.Next.Application.Ai.Workbench;

namespace AuswertungPro.Next.UI.ViewModels;

internal static class TrainingStudioPdfSourcePresentation
{
    public static string Format(WorkbenchItem? item)
    {
        var source = item?.SourceSuggestion;
        if (item is null || source is null)
            return string.Empty;

        var photo = string.IsNullOrWhiteSpace(source.PhotoId)
            ? string.Empty
            : $" · Foto {source.PhotoId}";
        var meter = item.IsStreckenschaden
            ? $"{item.MeterStart:0.00}–{item.MeterEnd:0.00} m"
            : $"{item.MeterStart:0.00} m";
        var date = item.InspectionDate.HasValue
            ? $" · Datum {item.InspectionDate.Value:dd.MM.yyyy}"
            : string.Empty;
        return $"Haltung {item.CaseId}{date}\n" +
               $"{source.SourceDocumentName} · Seite {source.PageNumber}{photo} · {meter}\n" +
               $"{source.VsaCode} — {source.Beschreibung}";
    }
}
