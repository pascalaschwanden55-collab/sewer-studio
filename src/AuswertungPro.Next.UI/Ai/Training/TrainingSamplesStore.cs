using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai.Training
{
    public static class TrainingSamplesStore
    {
        // Verhindert gleichzeitige Load+Save-Operationen (Race Condition bei MergeAndSaveAsync)
        private static readonly SemaphoreSlim _fileLock = new(1, 1);

        private static string GetStorePath() => KnowledgeRoot.GetTrainingSamplesPath();

        public static async Task<List<TrainingSample>> LoadAsync()
        {
            await _fileLock.WaitAsync();
            try
            {
                return await LoadInternalAsync();
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public static async Task SaveAsync(List<TrainingSample> samples)
        {
            await _fileLock.WaitAsync();
            try
            {
                await SaveInternalAsync(samples);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        /// <summary>
        /// Laedt bestehende Samples, mergt neue hinzu (Dedup via Signature), speichert.
        /// Verhindert Datenverlust bei wiederholtem Self-Training.
        /// Atomar: Load + Merge + Save unter einem Lock.
        /// </summary>
        public static async Task MergeAndSaveAsync(List<TrainingSample> newSamples)
        {
            await _fileLock.WaitAsync();
            try
            {
                var existing = await LoadInternalAsync();
                var existingSigs = existing
                    .Where(s => !string.IsNullOrEmpty(s.Signature))
                    .Select(s => s.Signature)
                    .ToHashSet(StringComparer.Ordinal);

                foreach (var s in newSamples)
                {
                    if (!string.IsNullOrEmpty(s.Signature) && existingSigs.Contains(s.Signature))
                        continue;
                    existing.Add(s);
                    if (!string.IsNullOrEmpty(s.Signature))
                        existingSigs.Add(s.Signature);
                }

                await SaveInternalAsync(existing);
                KnowledgeMirrorService.Current?.NotifyChanged();
            }
            finally
            {
                _fileLock.Release();
            }
        }

        /// <summary>
        /// Wie MergeAndSaveAsync, aber bei Signatur-Match wird das bestehende Sample
        /// in-place aktualisiert (Status, Notes, MatchLevel, KiCode).
        /// Verhindert Datenverlust bei Review-Korrekturen und manuellen Approvals.
        /// </summary>
        public static async Task MergeOrUpdateAsync(IEnumerable<TrainingSample> samples)
        {
            await _fileLock.WaitAsync();
            try
            {
                var existing = await LoadInternalAsync();
                var sigIndex = new Dictionary<string, int>(StringComparer.Ordinal);
                for (var i = 0; i < existing.Count; i++)
                {
                    if (!string.IsNullOrEmpty(existing[i].Signature))
                        sigIndex[existing[i].Signature] = i;
                }

                foreach (var s in samples)
                {
                    if (!string.IsNullOrEmpty(s.Signature) && sigIndex.TryGetValue(s.Signature, out var idx))
                    {
                        // In-place Update: Statusfelder + Anreicherungsfelder uebernehmen
                        var target = existing[idx];
                        target.Status = s.Status;
                        target.Notes = s.Notes;
                        target.MatchLevel = s.MatchLevel;
                        target.KiCode = s.KiCode;
                        target.KbIndexState = s.KbIndexState;
                        // Anreicherung: nur ueberschreiben wenn der neue Wert gesetzt ist
                        if (s.SourceType is not null) target.SourceType = s.SourceType;
                        if (s.TechniqueGrade is not null) target.TechniqueGrade = s.TechniqueGrade;
                        if (!string.IsNullOrWhiteSpace(s.Rohrmaterial)) target.Rohrmaterial = s.Rohrmaterial;
                        if (s.NennweiteMm.HasValue) target.NennweiteMm = s.NennweiteMm;
                        target.IsKorrigiert |= s.IsKorrigiert;
                        if (!string.IsNullOrWhiteSpace(s.QualityGateLevel)) target.QualityGateLevel = s.QualityGateLevel;
                    }
                    else
                    {
                        existing.Add(s);
                        if (!string.IsNullOrEmpty(s.Signature))
                            sigIndex[s.Signature] = existing.Count - 1;
                    }
                }

                await SaveInternalAsync(existing);
                KnowledgeMirrorService.Current?.NotifyChanged();
            }
            finally
            {
                _fileLock.Release();
            }
        }

        // ── Interne Methoden (ohne Lock, nur innerhalb von _fileLock aufrufen) ──

        private static async Task<List<TrainingSample>> LoadInternalAsync()
        {
            var path = GetStorePath();
            if (!File.Exists(path))
                return new List<TrainingSample>();

            try
            {
                using var stream = File.OpenRead(path);
                var samples = await JsonSerializer.DeserializeAsync<List<TrainingSample>>(stream);
                if (samples is null)
                    return new List<TrainingSample>();

                // Migration: Alte 3-teilige Signaturen (Code|Meter|MeterEnd) auf
                // 4-teilig (CaseId|Code|Meter|MeterEnd) umschreiben.
                // Einmalig beim ersten Laden nach dem Update.
                var migrated = false;
                foreach (var s in samples)
                {
                    if (string.IsNullOrEmpty(s.Signature) || string.IsNullOrEmpty(s.CaseId))
                        continue;
                    var parts = s.Signature.Split('|');
                    if (parts.Length == 3)
                    {
                        // Alt-Format: Code|Meter|MeterEnd → CaseId|Code|Meter|MeterEnd
                        s.Signature = $"{s.CaseId}|{s.Signature}";
                        migrated = true;
                    }
                }
                if (migrated)
                {
                    Debug.WriteLine("[TrainingSamplesStore] Signatur-Migration: 3-teilig → 4-teilig durchgefuehrt");
                    await SaveInternalAsync(samples);
                }

                return samples;
            }
            catch (Exception ex)
            {
                // Backup der korrupten Datei anlegen, aber nicht ueberschreiben —
                // falls mehrere Korruptionen passieren, bleibt das erste Backup erhalten.
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var backup = path + $".bad_{timestamp}";
                try { File.Copy(path, backup); }
                catch { /* best-effort Backup */ }

                Debug.WriteLine($"[TrainingSamplesStore] WARNUNG: JSON korrupt, Backup unter {backup}: {ex.Message}");

                // Versuche Backup-Dateien zu laden: .bak (juengstes Save-Backup) zuerst, dann .bad_* (Korruptions-Backups)
                var dir = Path.GetDirectoryName(path)!;
                var name = Path.GetFileName(path);
                var backups = new List<string>();
                // .bak zuerst (das ist das juengste erfolgreiche Save-Backup)
                var bakFile = path + ".bak";
                if (File.Exists(bakFile))
                    backups.Add(bakFile);
                // Dann .bad_* (Korruptions-Backups, juengstes zuerst)
                backups.AddRange(Directory.GetFiles(dir, name + ".bad_*")
                    .OrderByDescending(f => f));

                foreach (var bak in backups)
                {
                    try
                    {
                        using var bakStream = File.OpenRead(bak);
                        var bakSamples = await JsonSerializer.DeserializeAsync<List<TrainingSample>>(bakStream);
                        if (bakSamples is { Count: > 0 })
                        {
                            Debug.WriteLine($"[TrainingSamplesStore] Backup {Path.GetFileName(bak)} geladen: {bakSamples.Count} Samples");
                            return bakSamples;
                        }
                    }
                    catch { /* naechstes Backup versuchen */ }
                }

                Debug.WriteLine("[TrainingSamplesStore] KRITISCH: Kein lesbares Backup gefunden, starte mit leerer Liste");
                return new List<TrainingSample>();
            }
        }

        /// <summary>
        /// Atomar speichern: Erst in temp-Datei schreiben, dann umbenennen.
        /// Verhindert Datenverlust bei Absturz/Stromausfall waehrend des Schreibens.
        /// </summary>
        private static async Task SaveInternalAsync(List<TrainingSample> samples)
        {
            var path = GetStorePath();
            var dir = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(dir);

            // Rotierende Sicherheits-Backups vor dem Schreiben (.bak.3 → .bak.2 → .bak.1 → .bak)
            if (File.Exists(path))
            {
                try
                {
                    var bak1 = path + ".bak";
                    var bak2 = path + ".bak.2";
                    var bak3 = path + ".bak.3";
                    // Rotation: .bak.2 → .bak.3, .bak → .bak.2, aktuell → .bak
                    if (File.Exists(bak2)) { try { File.Copy(bak2, bak3, true); } catch { } }
                    if (File.Exists(bak1)) { try { File.Copy(bak1, bak2, true); } catch { } }
                    File.Copy(path, bak1, overwrite: true);
                }
                catch { /* best-effort */ }
            }

            // In temp-Datei schreiben (gleicher Ordner fuer atomares Rename)
            var tempPath = path + ".tmp";
            try
            {
                using (var stream = File.Create(tempPath))
                {
                    await JsonSerializer.SerializeAsync(stream, samples, Application.Common.JsonDefaults.Indented);
                    await stream.FlushAsync();
                }

                // Validierung: temp-Datei muss lesbar sein und gleiche Anzahl Samples haben
                using (var checkStream = File.OpenRead(tempPath))
                {
                    var check = await JsonSerializer.DeserializeAsync<List<TrainingSample>>(checkStream);
                    if (check is null || check.Count != samples.Count)
                        throw new InvalidOperationException(
                            $"Validierung fehlgeschlagen: erwartet {samples.Count}, gelesen {check?.Count ?? 0}");
                }

                // Atomares Rename: temp → Zieldatei
                File.Move(tempPath, path, overwrite: true);

                // Alte .bad_* Dateien aufraeumen (nur die letzten 3 behalten)
                CleanupBadFiles(path);
            }
            catch
            {
                // temp-Datei aufraeumen bei Fehler, Originaldatei bleibt unberuehrt
                try { if (File.Exists(tempPath)) File.Delete(tempPath); }
                catch { /* best-effort */ }
                throw; // Fehler weiterreichen damit der Aufrufer weiss dass Save fehlgeschlagen ist
            }
        }

        /// <summary>
        /// Raeumt alte .bad_* Backup-Dateien auf. Behaelt nur die letzten 3.
        /// Verhindert unbegrenztes Wachstum (vorher: 869 Dateien, 3.5 GB).
        /// </summary>
        private static void CleanupBadFiles(string basePath)
        {
            try
            {
                var dir = Path.GetDirectoryName(basePath)!;
                var name = Path.GetFileName(basePath);
                var badFiles = Directory.GetFiles(dir, name + ".bad_*")
                    .OrderByDescending(f => f)
                    .ToArray();

                // Nur die letzten 3 behalten
                for (int i = 3; i < badFiles.Length; i++)
                {
                    try { File.Delete(badFiles[i]); }
                    catch { /* best-effort */ }
                }

                if (badFiles.Length > 3)
                    Debug.WriteLine($"[TrainingSamplesStore] {badFiles.Length - 3} alte .bad Dateien aufgeraeumt");
            }
            catch { /* Aufraeumen ist optional */ }
        }
    }
}
