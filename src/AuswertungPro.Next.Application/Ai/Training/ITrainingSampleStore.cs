using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Abstraktion fuer das persistente Speichern und Laden von Trainingssamples.
/// Kapselt den statischen TrainingSamplesStore fuer testbare Abhaengigkeitsinjektion.
/// </summary>
public interface ITrainingSampleStore
{
    /// <summary>Laedt alle vorhandenen Trainingssamples.</summary>
    Task<List<TrainingSample>> LoadAsync();

    /// <summary>Ersetzt den gespeicherten Bestand atomar.</summary>
    Task SaveAsync(List<TrainingSample> samples);

    /// <summary>
    /// Aktualisiert vorhandene Samples in-place (per Signatur-Match) oder
    /// fuegt neue hinzu. Race-Condition-sicher (Load-Merge-Save unter Lock).
    /// </summary>
    Task MergeOrUpdateAsync(IEnumerable<TrainingSample> samples);

    /// <summary>
    /// Fuegt neue Samples hinzu (Dedup via Signatur, kein Ueberschreiben).
    /// Atomar: Load + Merge + Save unter einem Lock.
    /// </summary>
    Task MergeAndSaveAsync(List<TrainingSample> samples);
}
