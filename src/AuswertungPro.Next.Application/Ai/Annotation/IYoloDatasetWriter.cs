using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Ai.Training;

namespace AuswertungPro.Next.Application.Ai.Annotation;

/// <summary>
/// Adapter fuer Single-Sample-Append in den YOLO-seg-Datensatz
/// (D:/yolo_sewer_v1/images/{train|val}/*.png + labels/{train|val}/*.txt).
/// </summary>
public interface IYoloDatasetWriter
{
    /// <summary>
    /// Schreibt Frame.png + .txt-Label-Datei fuer ein einzelnes Sample.
    /// Der Polygon-String fuer YOLO-seg kommt aus
    /// <paramref name="preview"/>.<see cref="MaskPreview.PolygonJson"/> —
    /// daher muss der Writer beide Inputs kennen (B3).
    /// Wirft, wenn die Class-ID nicht stabil aufloesbar ist (Service muss
    /// vorher <c>VsaYoloClassMap.TryGetClassId</c> pruefen).
    /// </summary>
    /// <returns>Pfad zur geschriebenen Label-Datei.</returns>
    Task<string> AppendSampleAsync(
        TrainingSample sample,
        MaskPreview preview,
        CancellationToken ct);
}
