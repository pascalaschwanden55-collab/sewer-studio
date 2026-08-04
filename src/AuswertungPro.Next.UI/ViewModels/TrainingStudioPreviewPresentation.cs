using AuswertungPro.Next.Application.Ai.Training.Preview;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.UI.ViewModels;

/// <summary>
/// Formatiert ein reines Modelltestergebnis fuer die Anzeige. Es entsteht dabei
/// weder eine Hand-Box noch ein speicherbares Goldsample.
/// </summary>
internal sealed record TrainingStudioPreviewPresentation(
    IReadOnlyList<TrainingStudioPreviewDetectionItem> Detections,
    string Summary);

internal static class TrainingStudioPreviewPresenter
{
    public static TrainingStudioPreviewPresentation Build(
        TrainingPreviewDetectionResult result,
        TrainingStudioPreviewModelOption model,
        Func<string, string> resolveCodeLabel)
    {
        if (!result.Available)
        {
            var unavailableSummary = string.IsNullOrWhiteSpace(result.Error)
                ? $"{model.DisplayName}: Modell ist nicht verfuegbar."
                : $"{model.DisplayName}: {result.Error}";
            return new TrainingStudioPreviewPresentation([], unavailableSummary);
        }

        if (!result.FrameUsable)
        {
            var reason = string.IsNullOrWhiteSpace(result.QualityReason)
                ? "Qualitaetspruefung fehlgeschlagen"
                : result.QualityReason;
            return new TrainingStudioPreviewPresentation(
                [],
                $"{model.DisplayName}: Foto nicht geprueft ({reason}). "
                + "Das ist kein Negativtreffer; nichts gespeichert.");
        }

        var detections = result.Detections
            .Select(detection =>
            {
                var code = YoloClassVsaMapper.ToPersistableVsaCode(
                    detection.ClassName);
                var text = string.IsNullOrWhiteSpace(code)
                    ? detection.ClassName
                    : $"{code} — {resolveCodeLabel(code)}";
                return new TrainingStudioPreviewDetectionItem(
                    detection.X1,
                    detection.Y1,
                    detection.X2,
                    detection.Y2,
                    text,
                    detection.Confidence);
            })
            .ToArray();
        var summary = detections.Length == 0
            ? $"{model.DisplayName}: kein Treffer. Nur Vorschau — nichts gespeichert."
            : $"{model.DisplayName}: {detections.Length} Treffer. "
              + "Blaue Boxen sind nur Vorschau und werden nicht gespeichert.";
        return new TrainingStudioPreviewPresentation(detections, summary);
    }
}
