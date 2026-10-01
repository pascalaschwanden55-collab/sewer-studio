using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Application.UseCases.GoldSampleSpeichern;

/// <summary>
/// Phase "Sample dauerhaft speichern": legt ein neues Sample an oder ersetzt den geladenen
/// Bestand unter derselben SampleId. Nur die beiden Reparaturwege bereinigen danach alte
/// KB-/Teacher-Ableitungen; Fehler dieser Bereinigung sind Warnungen, keine Ablehnung.
/// </summary>
internal sealed class GoldSampleAblage
{
    private readonly ITrainingSampleStore _sampleStore;
    private readonly IKnowledgeBaseIndexer _kbIndexer;
    private readonly ITeacherAnnotationStore _teacherStore;

    public GoldSampleAblage(
        ITrainingSampleStore sampleStore,
        IKnowledgeBaseIndexer kbIndexer,
        ITeacherAnnotationStore teacherStore)
    {
        _sampleStore = sampleStore;
        _kbIndexer = kbIndexer;
        _teacherStore = teacherStore;
    }

    /// <summary>
    /// Speichert ein neues Sample oder ersetzt den geladenen Bestand. Ein Schreibfehler wird
    /// in allen Wegen als dieselbe Ablehnung gemeldet, ein Abbruch weitergeworfen. Liefert
    /// eine Ablehnung nur, solange das Sample noch nicht gespeichert ist.
    /// </summary>
    public async Task<(string? Warning, WorkbenchSaveResult? Rejection)> PersistAsync(
        WorkbenchItem item,
        TrainingSample sample,
        bool repairsExistingSample,
        bool codeChanged,
        CancellationToken ct)
    {
        string? replaceWarning = null;
        if (repairsExistingSample && codeChanged)
        {
            try
            {
                replaceWarning = await ReplaceSampleWithChangedCodeAsync(item, sample).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return (null, Speicherfehler(ex));
            }
        }
        else if (repairsExistingSample)
        {
            // Gezielt geladenes, unvollstaendiges Goldsample um Box/Segmentierung ergaenzen.
            // So entsteht beim Nachlabeln kein doppelter Datensatz.
            try
            {
                var replaced = await _sampleStore.ReplaceBySampleIdAsync(sample).ConfigureAwait(false);
                if (!replaced)
                {
                    var added = await _sampleStore.TryAddNewAsync(sample, ct).ConfigureAwait(false);
                    if (!added)
                    {
                        return (null, GoldSampleAufbau.Rejected(
                            "Goldsample wurde nicht gespeichert: Die Signatur gehört bereits zu einem anderen Datensatz."));
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return (null, Speicherfehler(ex));
            }

            // Auch ein Nachlabeln mit gleichem Code ersetzt die fachliche Wahrheit
            // (neue Box/Signatur). Alte KB-/Teacher-Ableitungen derselben SampleId
            // muessen deshalb vor dem Neuaufbau entfernt werden.
            replaceWarning = await ReplaceSampleWithChangedCodeAsync(
                    item,
                    sample,
                    sampleAlreadyReplaced: true)
                .ConfigureAwait(false);
        }
        else
        {
            // Neuanlage mit eindeutigem Ergebnis: bei Signatur-Dublett NICHT still
            // weiterlaufen — sonst entstuenden KB-/Teacher-Eintraege ohne JSON-Sample
            // (Waisen). Die inhaltsadressierte Goldkopie ist bei echten Duplikaten
            // ohnehin dieselbe Datei (kein Muell). Ein Speicherfehler wird seit 01.10.2026
            // wie in den Reparaturwegen als Ablehnung gemeldet; ein Abbruch wird weitergeworfen.
            // Der Store schreibt ueber Temp-Datei und atomares Ersetzen, ein Fehler hinterlaesst
            // also kein halbes Sample.
            bool added;
            try
            {
                added = await _sampleStore.TryAddNewAsync(sample, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return (null, Speicherfehler(ex));
            }
            if (!added)
            {
                return (null, GoldSampleAufbau.Rejected(
                    "Bereits als Goldsample vorhanden (gleiche Haltung, Code, Meter und Box). Zum Ändern den Eintrag über 'Unvollständige Goldframes' oder das Goldalbum laden."));
            }
        }

        return (replaceWarning, null);
    }

    /// <summary>
    /// Gemeinsame Ablehnung fuer einen Schreibfehler in allen drei Wegen (Neuanlage,
    /// Nachlabeln, Ersatz bei geaendertem Code): gleicher Text, gleicher Ergebnistyp.
    /// </summary>
    private static WorkbenchSaveResult Speicherfehler(Exception ex)
        => GoldSampleAufbau.Rejected(
            $"Goldsample konnte nicht gespeichert werden: {UserError.DescribeAndReport(ex, "Goldsample speichern")}");

    /// <summary>
    /// Ersetzt ein Bestandssample bei geaenderter Code-Entscheidung (gleiche SampleId, neuer
    /// Code/Ordner): alten Eintrag loeschen, neuen anhaengen, danach den alten KB-Eintrag und
    /// den alten Teacher-Kandidaten entfernen. Das KB-Deindex liegt bewusst VOR dem neuen
    /// Index (KB-Nachtrag), damit nicht der frisch geschriebene Eintrag geloescht wird.
    /// Fehler bei den Bereinigungen machen den Save NICHT rueckgaengig — sie werden als
    /// sichtbare Warnung zurueckgegeben (Muster wie KB-/Teacher-Warnung, nie still).
    /// </summary>
    private async Task<string?> ReplaceSampleWithChangedCodeAsync(
        WorkbenchItem item,
        TrainingSample sample,
        bool sampleAlreadyReplaced = false)
    {
        // Atomares Ersetzen unter einer Sperre (Loeschen + Anhaengen + Speichern in einem
        // Schritt). Existiert die Id nicht (z. B. zwischenzeitlich geloescht), wird der Fund
        // als Neuanlage zusammengefuehrt, damit er nicht verloren geht.
        if (!sampleAlreadyReplaced)
        {
            var replaced = await _sampleStore.ReplaceBySampleIdAsync(sample).ConfigureAwait(false);
            if (!replaced)
            {
                var added = await _sampleStore.TryAddNewAsync(sample).ConfigureAwait(false);
                if (!added)
                {
                    throw new UserFacingException(
                        "Die Signatur gehört bereits zu einem anderen Gold-Datensatz.");
                }
            }
        }

        // KB: alten Code-Eintrag entfernen (gleiche SampleId, alter Code-Inhalt).
        string? warning = null;
        try
        {
            _kbIndexer.Deindex(sample.SampleId);
        }
        catch (Exception ex)
        {
            warning = $"Alter KB-Eintrag konnte nicht entfernt werden: {UserError.DescribeAndReport(ex, "Alten KB-Eintrag entfernen")}";
        }

        // Teacher: alten Kandidaten entfernen — sonst lernt der Export weiter den alten Code.
        // Primaertreffer ueber den Fremdschluessel SourceSampleId (Neubestand). Altbestand
        // ohne SourceSampleId: ueber Goldpfad (item.FramePath ist beim Reparatur-Laden der
        // gespeicherte Goldpfad) oder fachliche Signatur (alter Code + Meter + Haltung) —
        // aber NUR bei GENAU EINEM Kandidaten; bei Mehrdeutigkeit nichts loeschen, sondern
        // sichtbar warnen (nie still das Falsche entfernen).
        try
        {
            var oldHaltung = item.HaltungName ?? item.CaseId;
            var candidates = await _teacherStore.LoadAsync().ConfigureAwait(false);
            var stale = candidates
                .Where(annotation =>
                    string.Equals(annotation.SourceSampleId, sample.SampleId, StringComparison.Ordinal))
                .ToList();

            var legacy = candidates
                .Where(annotation => annotation.SourceSampleId is null)
                .Where(annotation =>
                    GoldSampleAufbau.PathsEqual(annotation.FullFramePath, item.FramePath)
                    || (string.Equals(annotation.VsaCode, item.ExistingCode, StringComparison.OrdinalIgnoreCase)
                        && annotation.MeterPosition == item.MeterStart
                        && string.Equals(annotation.HaltungName, oldHaltung, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (legacy.Count == 1)
                stale.AddRange(legacy);
            else if (legacy.Count > 1)
                warning = GoldSampleAufbau.CombineWarnings(
                    warning,
                    $"{legacy.Count} alte Teacher-Einträge unklar zugeordnet — bitte manuell prüfen.");

            foreach (var annotation in stale)
                await _teacherStore.DeleteAsync(annotation.AnnotationId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            warning = GoldSampleAufbau.CombineWarnings(
                warning,
                $"Alter Teacher-Eintrag konnte nicht entfernt werden: {UserError.DescribeAndReport(ex, "Alten Teacher-Eintrag entfernen")}");
        }

        return warning;
    }
}
