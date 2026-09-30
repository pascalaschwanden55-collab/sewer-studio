using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;

namespace AuswertungPro.Next.Application.UseCases.GoldSampleSpeichern;

/// <summary>
/// Eine persoenlich bestaetigte Box, die als Goldsample gespeichert werden soll.
/// <see cref="ImageSnapshot"/> ist nur beim Foto-Assistenten gesetzt: Eval-Pruefung und
/// Goldkopie verwenden dann genau diese Bytes und lesen den Quellpfad nicht erneut.
/// </summary>
public sealed record GoldSampleSpeichernAnfrage(
    WorkbenchItem Item,
    BoundingBox Box,
    WorkbenchSegmentation? Segmentation,
    WorkbenchDecision Decision,
    WorkbenchImageSnapshot? ImageSnapshot = null);

/// <summary>
/// Geladene Eval-Schutzdaten (Bild-Hashes und Haltungskennungen des eingefrorenen
/// Mess-Sets). Das Laden selbst bleibt in der aufrufenden Schicht.
/// </summary>
public sealed record GoldSampleEvalSchutz(
    IReadOnlySet<string> ImageHashes,
    IReadOnlySet<string> HaltungKeys);

/// <summary>
/// Ergebnis der Goldmaskenpruefung. Nur eine gueltige Maske darf auf das Sample
/// uebertragen werden; eine abgelehnte Maske bleibt weg.
/// </summary>
public interface IGoldSampleMaske
{
    bool IsValid { get; }

    void ApplyTo(TrainingSample sample);
}

/// <summary>
/// Prueft die SAM-Maske gegen Hand-Box und gespeichertes Goldbild. Die strenge
/// Pruefung (Degraded, RLE, Boxregel, Bildmasse) liegt ausserhalb der Application-Schicht.
/// </summary>
public delegate IGoldSampleMaske GoldSampleMaskenPruefung(
    WorkbenchSegmentation? segmentation,
    BoundingBox box,
    string storedFramePath);

/// <summary>
/// Zustand NACH der dauerhaften Sample-Speicherung. Ein Wert dieses Typs entsteht nur,
/// wenn das Sample in <c>training_samples.json</c> steht. Alles, was danach folgt
/// (KB-Nachtrag, Teacher-Nachlauf), darf nur noch einen gespeicherten Zustand melden,
/// bei Fehlern mit Warnung, nie "nicht gespeichert".
/// </summary>
internal sealed record DauerhaftGespeichertesGoldSample(
    TrainingSample Sample,
    string SampleId,
    string StoredFramePath,
    string StoredImageSha256,
    bool GoldApproved,
    string? ErsatzWarnung);
