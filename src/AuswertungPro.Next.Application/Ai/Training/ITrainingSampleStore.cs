using System.Collections.Generic;
using System.Threading;
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

    /// <summary>
    /// Fuegt ein neues Sample nur an, wenn kein Bestandseintrag mit derselben Signatur
    /// existiert. True = angehaengt; false = per Signatur-Dedup uebersprungen (Bestand
    /// inhaltlich unveraendert). Atomar unter demselben Lock wie MergeAndSaveAsync —
    /// der Aufrufer erfaehrt so eindeutig, ob sein Speichern wirklich angelegt hat
    /// (kein stilles Ueberspringen, das KB-/Teacher-Waisen erzeugen koennte).
    /// </summary>
    Task<bool> TryAddNewAsync(TrainingSample sample, CancellationToken ct = default);

    /// <summary>
    /// Entfernt das Sample mit der gegebenen SampleId vollstaendig aus dem Bestand.
    /// True, wenn ein Eintrag entfernt wurde. Noetig beim Ersetzen mit geaendertem Code:
    /// der Merge-Schluessel ist die Signatur (enthaelt den Code) — ein Code-Wechsel ist
    /// daher kein Update, sondern Loeschen + Neuanlage unter gleicher SampleId.
    /// </summary>
    Task<bool> RemoveBySampleIdAsync(string sampleId);

    /// <summary>
    /// Ersetzt das Sample mit derselben SampleId atomar unter EINER Sperre:
    /// alten Eintrag entfernen, neues Sample anhaengen, einmal speichern.
    /// False, wenn die SampleId nicht existiert — dann wird NICHTS geschrieben
    /// (der Aufrufer behandelt den Fall).
    /// </summary>
    Task<bool> ReplaceBySampleIdAsync(TrainingSample sample);
}
