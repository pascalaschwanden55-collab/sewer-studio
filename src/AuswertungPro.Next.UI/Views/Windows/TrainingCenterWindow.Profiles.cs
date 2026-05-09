using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

using Infrastructure = AuswertungPro.Next.Infrastructure;

namespace AuswertungPro.Next.UI.Views.Windows;

// Inspektions-Profile aus WinCan/KIAS/IBAK extrahieren + optionale Frame-
// Extraktion fuer Trainings-Daten. Aus dem Hauptdatei extrahiert (Slice 9b).
public partial class TrainingCenterWindow
{
    // ── Inspektions-Profile extrahieren ──────────────────────────────────

    private async void ExtractProfiles_Click(object sender, RoutedEventArgs e)
    {
        // Sofort sichtbares Feedback - sonst denkt der User nichts passiere.
        try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Profile-Extraktion: Datei waehlen...\n"); } catch { }

        // Quelle waehlen:
        //   - WinCan: DB3 (SQLite) / SDF (SQL Server Compact) / SQLite
        //   - KIAS/IBAK: Arizona.fdb (Firebird), Daten.txt (IBAK-Beobachtungen), *.xtf (ISYBAU)
        // ACHTUNG: "Daten.txt" als Pattern wird vom Windows-File-Picker nur erkannt
        // wenn ein Wildcard davorsteht. Daher "*Daten.txt" + zusaetzlich "*.txt".
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Datenquelle waehlen (WinCan DB3/SDF/SQLite oder KIAS/IBAK FDB/Daten.txt/XTF)",
            Filter = "Alle unterstuetzten|*.db3;*.sdf;*.sqlite;*.fdb;*Daten.txt;*.xtf|"
                   + "WinCan DB3 (SQLite)|*.db3|"
                   + "WinCan SDF (SQL Server Compact)|*.sdf|"
                   + "SQLite|*.sqlite|"
                   + "KIAS Arizona.fdb (Firebird)|*.fdb|"
                   + "IBAK Daten.txt|*Daten.txt;*.txt|"
                   + "ISYBAU XTF|*.xtf|"
                   + "Alle Dateien|*.*",
            Multiselect = false
        };

        bool? dlgResult;
        try { dlgResult = dlg.ShowDialog(); }
        catch (Exception dex)
        {
            _dialogs.ShowMessage($"Dialog konnte nicht geoeffnet werden:\n{dex.Message}",
                "Profile extrahieren", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        if (dlgResult != true)
        {
            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Auswahl abgebrochen.\n"); } catch { }
            return;
        }

        var selectedPath = dlg.FileName;
        string db3Path;

        try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Gewaehlt: {selectedPath}\n"); } catch { }

        // KIAS/IBAK-Pfad: wenn Arizona.fdb, Daten.txt oder *.xtf gewaehlt -> IBAK-Profile
        // direkt aus Daten.txt + Stammdaten-Aggregator (XTF/PDF/FDB) extrahieren.
        var ext = System.IO.Path.GetExtension(selectedPath);
        var fileName = System.IO.Path.GetFileName(selectedPath);
        var isKiasSource = string.Equals(ext, ".fdb", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(ext, ".xtf", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(fileName, "Daten.txt", StringComparison.OrdinalIgnoreCase);
        if (isKiasSource)
        {
            try
            {
                await ExtractProfilesFromKiasAsync(selectedPath);
            }
            catch (Exception kex)
            {
                try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] KIAS-FEHLER (vor Try): {kex.Message}\n"); } catch { }
                _dialogs.ShowMessage($"KIAS/IBAK-Extraktion fehlgeschlagen:\n{kex}", "Profile extrahieren",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return;
        }

        // Bei SDF automatisch in SQLite konvertieren bevor der Extractor drauf zugreift.
        if (selectedPath.EndsWith(".sdf", StringComparison.OrdinalIgnoreCase))
        {
            if (!Infrastructure.Import.WinCan.SdfToSqliteConverter.IsSsceAvailable())
            {
                _dialogs.ShowMessage(
                    "SDF-Konvertierung nicht moeglich: SQL Server Compact 4.0 Runtime fehlt.\n\n" +
                    "Installieren via 'Microsoft SQL Server Compact 4.0 SP1' " +
                    "(download.microsoft.com, SSCERuntime_x64-ENU.exe).",
                    "SDF nicht unterstuetzt",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnExtractProfiles.IsEnabled = false;
            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] SDF wird nach SQLite konvertiert: {System.IO.Path.GetFileName(selectedPath)}...\n"); } catch { }
            try
            {
                db3Path = await System.Threading.Tasks.Task.Run(() =>
                    Infrastructure.Import.WinCan.SdfToSqliteConverter.Convert(selectedPath));
                try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Konvertierung fertig: {db3Path}\n"); } catch { }
            }
            catch (Exception ex)
            {
                try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] SDF-Konvertierung FEHLGESCHLAGEN: {ex.Message}\n"); } catch { }
                _dialogs.ShowMessage($"SDF-Konvertierung fehlgeschlagen:\n\n{ex.Message}",
                    "SDF-Konvertierung", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnExtractProfiles.IsEnabled = true;
                return;
            }
        }
        else
        {
            db3Path = selectedPath;
        }
        var outputDir = System.IO.Path.Combine(@"C:\KI_BRAIN", "inspection_profiles");
        var patternsPath = System.IO.Path.Combine(@"C:\KI_BRAIN", "inspection_patterns.json");

        BtnExtractProfiles.IsEnabled = false;
        try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Profile extrahieren: {System.IO.Path.GetFileName(db3Path)}...\n"); } catch { }

        try
        {
            var profiles = await System.Threading.Tasks.Task.Run(() =>
                Infrastructure.Import.WinCan.InspectionProfileExtractor.ExtractFromDb3(db3Path));

            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] {profiles.Count} Profile extrahiert\n"); } catch { }

            // Profile speichern
            await System.Threading.Tasks.Task.Run(() =>
                Infrastructure.Import.WinCan.InspectionProfileExtractor.SaveProfiles(profiles, outputDir));

            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Profile gespeichert: {outputDir}\n"); } catch { }

            // Muster aggregieren
            var patterns = Infrastructure.Import.WinCan.InspectionPatternAggregator.Aggregate(profiles);
            await System.Threading.Tasks.Task.Run(() =>
                Infrastructure.Import.WinCan.InspectionPatternAggregator.SavePatterns(patterns, patternsPath));

            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Muster aggregiert:\n"); } catch { }
            try { Vm?.AppendToLogText($"  Haltungen: {patterns.AnzahlHaltungen}, Beobachtungen: {patterns.AnzahlBeobachtungen}\n"); } catch { }
            try { Vm?.AppendToLogText($"  Median Geschwindigkeit: {patterns.MedianFahrgeschwindigkeit:F3} m/s\n"); } catch { }
            try { Vm?.AppendToLogText($"  Median Codierungen/m: {patterns.MedianCodierungenProMeter:F2}\n"); } catch { }
            try { Vm?.AppendToLogText($"  Median Luecke: {patterns.MedianLueckeMeter:F1}m\n"); } catch { }
            foreach (var r in patterns.SequenzRegeln)
                try { Vm?.AppendToLogText($"  Regel: {r.Regel} (Support: {r.Support:P0}, Ausnahmen: {r.Ausnahmen})\n"); } catch { }

            // QualityFlags zusammenfassen
            int warnCount = profiles.Count(p => p.QualityFlags.Warnings.Count > 0);
            int noBcd = profiles.Count(p => p.QualityFlags.MissingBcd);
            int noBce = profiles.Count(p => p.QualityFlags.MissingBce);
            try { Vm?.AppendToLogText($"  Warnungen: {warnCount} Profile, fehlendes BCD: {noBcd}, fehlendes BCE: {noBce}\n"); } catch { }

            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Profile fertig. Gespeichert nach {outputDir}\n"); } catch { }

            // Fragen ob Frames extrahiert werden sollen.
            // Bei SDF-Quelle verwenden wir den Original-SDF-Pfad fuer die Video-Suche —
            // die konvertierte .db3 liegt in C:\KI_BRAIN\sdf_converted\, dort sind keine Videos.
            var rootBaseForVideos = selectedPath;
            var exportRoot = System.IO.Path.GetDirectoryName(rootBaseForVideos) ?? "";
            // WinCan-Export-Root: 2 Ebenen hoch von DB-Ordner (DB → Project → DISK1)
            var disk1Root = exportRoot;
            for (int up = 0; up < 3; up++)
            {
                var parent = System.IO.Path.GetDirectoryName(disk1Root);
                if (parent != null) disk1Root = parent;
            }

            // Profile mit Video zaehlen
            int mitVideo = 0;
            foreach (var p in profiles)
            {
                if (string.IsNullOrEmpty(p.VideoPfad)) continue;
                var resolved = Infrastructure.Import.WinCan.VideoResolver.Resolve(
                    p.HaltungKey, disk1Root,
                    string.IsNullOrEmpty(p.VideoPfad) ? null : new List<string> { p.VideoPfad });
                if (resolved != null) mitVideo++;
            }

            var framesDir = System.IO.Path.Combine(@"C:\KI_BRAIN", "training_frames");
            int geschaetzteFrames = profiles.Sum(p => p.Ereignisse.Count * 5 + p.Luecken.Count(l => (l.DistanzM ?? 0) > 3) + 2);

            var extractFrames = _dialogs.ShowMessage(
                $"{profiles.Count} Profile extrahiert.\n" +
                $"{mitVideo} davon haben ein Video.\n\n" +
                $"Geschwindigkeit: {patterns.MedianFahrgeschwindigkeit:F3} m/s\n" +
                $"Codierungen/m: {patterns.MedianCodierungenProMeter:F2}\n\n" +
                $"Jetzt ~{geschaetzteFrames} Trainings-Frames aus den Videos extrahieren?\n" +
                $"(5 Frames pro Codierung + Negativ-Beispiele + Aufnahmetechnik)\n" +
                $"Geschaetzte Dauer: ~{mitVideo * 30 / 60} Minuten",
                "Frames extrahieren?", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (extractFrames == MessageBoxResult.Yes && mitVideo > 0)
            {
                await ExtractFramesFromProfiles(profiles, disk1Root, framesDir);
            }
        }
        catch (Exception ex)
        {
            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] FEHLER: {ex.Message}\n"); } catch { }
            _dialogs.ShowMessage($"Fehler: {ex.Message}", "Profile extrahieren", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnExtractProfiles.IsEnabled = true;
        }
    }

    /// <summary>
    /// Extrahiert Inspektionsprofile aus einem KIAS/IBAK-Export. Akzeptiert die
    /// Auswahl von Arizona.fdb, Daten.txt oder *.xtf - in allen Faellen wird der
    /// daruebergeordnete Export-Ordner als Wurzel genutzt und der
    /// IbakInspectionProfileExtractor (Daten.txt + StammdatenAggregator) angewendet.
    /// </summary>
    private async System.Threading.Tasks.Task ExtractProfilesFromKiasAsync(string selectedFile)
    {
        try { BtnExtractProfiles.IsEnabled = false; } catch { }
        // Sofort sichtbares Feedback bevor irgendetwas crashen kann.
        try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] KIAS-Extraktion startet fuer {System.IO.Path.GetFileName(selectedFile)}...\n"); } catch { }
        try
        {
            // Export-Wurzel aus Datei-Pfad ableiten:
            //   - Arizona.fdb liegt in <root>/Data/Arizona.fdb -> root = parent von Data
            //   - Daten.txt   liegt in <root>/Film/Daten.txt    -> root = parent von Film
            //   - *.xtf       liegt typisch in <root> selbst    -> root = Verzeichnis der xtf
            var dir = System.IO.Path.GetDirectoryName(selectedFile) ?? "";
            var parent = System.IO.Path.GetDirectoryName(dir);
            var folderName = System.IO.Path.GetFileName(dir);
            var exportRoot = (string.Equals(folderName, "Data", StringComparison.OrdinalIgnoreCase)
                          ||  string.Equals(folderName, "Film", StringComparison.OrdinalIgnoreCase))
                          && !string.IsNullOrWhiteSpace(parent)
                ? parent!
                : dir;

            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] KIAS/IBAK-Quelle erkannt: {System.IO.Path.GetFileName(selectedFile)}\n"); } catch { }
            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Export-Wurzel: {exportRoot}\n"); } catch { }

            var pattern = Infrastructure.Import.Ibak.KiasExportPattern.Detect(exportRoot);
            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] KIAS-Pattern: {(pattern.IsKias ? "ja" : "nein")} ({pattern.Reason})\n"); } catch { }

            var profiles = await System.Threading.Tasks.Task.Run(() =>
                Infrastructure.Import.Ibak.IbakInspectionProfileExtractor.ExtractFromExportRoot(exportRoot));

            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] {profiles.Count} Profile aus IBAK Daten.txt extrahiert\n"); } catch { }
            if (profiles.Count == 0)
            {
                _dialogs.ShowMessage($"Keine Inspektionsprofile gefunden.\nExport-Wurzel: {exportRoot}\n\n"
                    + "Pruefe ob Film/Daten.txt vorhanden ist.",
                    "Profile extrahieren", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var outputDir = System.IO.Path.Combine(@"C:\KI_BRAIN", "inspection_profiles");
            var patternsPath = System.IO.Path.Combine(@"C:\KI_BRAIN", "inspection_patterns.json");

            await System.Threading.Tasks.Task.Run(() =>
                Infrastructure.Import.WinCan.InspectionProfileExtractor.SaveProfiles(profiles, outputDir));
            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Profile gespeichert: {outputDir}\n"); } catch { }

            var aggPatterns = Infrastructure.Import.WinCan.InspectionPatternAggregator.Aggregate(profiles);
            await System.Threading.Tasks.Task.Run(() =>
                Infrastructure.Import.WinCan.InspectionPatternAggregator.SavePatterns(aggPatterns, patternsPath));

            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Muster aggregiert:\n"); } catch { }
            try { Vm?.AppendToLogText($"  Haltungen: {aggPatterns.AnzahlHaltungen}, Beobachtungen: {aggPatterns.AnzahlBeobachtungen}\n"); } catch { }
            try { Vm?.AppendToLogText($"  Median Geschwindigkeit: {aggPatterns.MedianFahrgeschwindigkeit:F3} m/s\n"); } catch { }
            try { Vm?.AppendToLogText($"  Median Codierungen/m: {aggPatterns.MedianCodierungenProMeter:F2}\n"); } catch { }

            int mitVideo = profiles.Count(p => !string.IsNullOrEmpty(p.VideoPfad));
            int ohneLaenge = profiles.Count(p => p.LaengeM is null);
            int totalEvents = profiles.Sum(p => p.Ereignisse.Count);
            try { Vm?.AppendToLogText($"  Profile mit Video: {mitVideo}/{profiles.Count}, ohne Laenge: {ohneLaenge}\n"); } catch { }
            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] KIAS/IBAK-Profile fertig.\n"); } catch { }

            _dialogs.ShowMessage(
                $"KIAS/IBAK-Extraktion erfolgreich:\n\n"
                + $"  - {profiles.Count} Inspektionsprofile\n"
                + $"  - {totalEvents} Beobachtungen\n"
                + $"  - {mitVideo}/{profiles.Count} mit Video-Zuordnung\n"
                + $"  - {profiles.Count - ohneLaenge}/{profiles.Count} mit Haltungslaenge\n\n"
                + $"Profile gespeichert nach:\n{outputDir}\n\n"
                + $"Muster-JSON:\n{patternsPath}",
                "Profile extrahiert",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] FEHLER: {ex.Message}\n"); } catch { }
            _dialogs.ShowMessage($"Fehler: {ex.Message}", "Profile extrahieren", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnExtractProfiles.IsEnabled = true;
        }
    }

    /// <summary>Extrahiert Trainings-Frames aus Videos fuer alle Profile mit Video-Zuordnung.</summary>
    private async System.Threading.Tasks.Task ExtractFramesFromProfiles(
        List<Infrastructure.Import.WinCan.InspectionProfile> profiles,
        string exportRoot,
        string framesDir)
    {
        var allFrames = new List<Infrastructure.Import.WinCan.ExtractedFrame>();
        int done = 0;
        int total = profiles.Count;
        var cts = new System.Threading.CancellationTokenSource();

        Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Frame-Extraktion gestartet ({total} Haltungen)...\n");

        foreach (var profile in profiles)
        {
            done++;

            // Video finden
            var dbFiles = string.IsNullOrEmpty(profile.VideoPfad) ? null : new List<string> { profile.VideoPfad };
            var videoMatch = Infrastructure.Import.WinCan.VideoResolver.Resolve(
                profile.HaltungKey, exportRoot, dbFiles);

            if (videoMatch == null)
            {
                Vm.AppendToLogText($"  [{done}/{total}] {profile.HaltungKey}: Kein Video gefunden — uebersprungen\n");
                continue;
            }

            Vm.AppendToLogText($"  [{done}/{total}] {profile.HaltungKey}: {profile.Ereignisse.Count} Events, Video: {System.IO.Path.GetFileName(videoMatch.FilePath)} (Conf: {videoMatch.Confidence:F2})\n");

            try
            {
                var frames = await Infrastructure.Import.WinCan.InspectionFrameExtractor.ExtractFramesAsync(
                    profile, videoMatch.FilePath, framesDir, cts.Token);

                allFrames.AddRange(frames);
                Vm.AppendToLogText($"    → {frames.Count} Frames extrahiert\n");
            }
            catch (Exception ex)
            {
                Vm.AppendToLogText($"    → FEHLER: {ex.Message}\n");
            }
        }

        // Frame-Index kumulieren (bestehende laden + neue dazufuegen)
        var indexPath = System.IO.Path.Combine(framesDir, "_frame_index.json");
        var existingFrames = new List<Infrastructure.Import.WinCan.ExtractedFrame>();
        if (System.IO.File.Exists(indexPath))
        {
            try
            {
                var json = await System.IO.File.ReadAllTextAsync(indexPath);
                var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                existingFrames = System.Text.Json.JsonSerializer.Deserialize<List<Infrastructure.Import.WinCan.ExtractedFrame>>(json, opts)
                    ?? new();
            }
            catch { /* Korrupter Index → neu erstellen */ }
        }

        // Duplikate entfernen (gleicher Pfad = gleicher Frame)
        var existingPaths = new HashSet<string>(existingFrames.Select(f => f.PngPfad), StringComparer.OrdinalIgnoreCase);
        var newFrames = allFrames.Where(f => !existingPaths.Contains(f.PngPfad)).ToList();
        existingFrames.AddRange(newFrames);

        Vm.AppendToLogText($"  Index: {newFrames.Count} neue + {existingPaths.Count} bestehende = {existingFrames.Count} total\n");

        await System.Threading.Tasks.Task.Run(() =>
            Infrastructure.Import.WinCan.InspectionFrameExtractor.SaveFrameIndex(existingFrames, indexPath));

        allFrames = existingFrames;

        // Zusammenfassung
        int refFrames = allFrames.Count(f => f.IsReferenceFrame);
        int negFrames = allFrames.Count(f => f.FrameTyp.Contains("negativ"));
        int techFrames = allFrames.Count(f => f.Quelle == "aufnahmetechnik");

        Vm.AppendToLogText($"\n[{DateTime.Now:HH:mm:ss}] Frame-Extraktion abgeschlossen:\n");
        Vm.AppendToLogText($"  Total: {allFrames.Count} Frames\n");
        Vm.AppendToLogText($"  Referenz-Frames (Codierungen): {refFrames}\n");
        Vm.AppendToLogText($"  Negativ-Beispiele: {negFrames}\n");
        Vm.AppendToLogText($"  Aufnahmetechnik: {techFrames}\n");
        Vm.AppendToLogText($"  Gespeichert: {framesDir}\n");
        Vm.AppendToLogText($"  Index: {indexPath}\n");

        _dialogs.ShowMessage(
            $"{allFrames.Count} Frames extrahiert!\n\n" +
            $"Referenz: {refFrames}\n" +
            $"Negativ: {negFrames}\n" +
            $"Aufnahmetechnik: {techFrames}\n\n" +
            $"Gespeichert: {framesDir}",
            "Frame-Extraktion", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
