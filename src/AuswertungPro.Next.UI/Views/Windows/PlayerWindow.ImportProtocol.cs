using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.ViewModels.Protocol;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AppProtocol = AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.Views.Windows;

// Phase 6.1.G: Import + Protocol-Vorschau extrahiert aus PlayerWindow.xaml.cs.
//
// Enthaelt das Annehmen importierter PDF-Eintraege (Doppelklick, Seek, Confirm),
// das Laden bestehender Protokoll-Eintraege beim Window-Open, das Synchronisieren
// von CodingEvents mit DamageMarkern und die Protokoll-Vorschau nach Codierung.
public partial class PlayerWindow
{
    private bool _isImportEditorOpen;

    private void ImportEvents_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_isImportEditorOpen) return;
        if (LstImportEvents.SelectedItem is not CodingEvent importEvent) return;
        SeekToImportEvent(importEvent);

        // 2026-05-11 User-Wunsch: Doppelklick auf bestehende codierte
        // Beobachtung soll den Edit-Dialog mit Pre-Population oeffnen
        // (analog CodingEvents_DoubleClick in PlayerWindow.CodingEvents.cs).
        // Damit kann der User die Codierung bestaetigen oder korrigieren,
        // statt von vorne anzufangen.
        OpenImportEditorForExistingEvent(importEvent);
    }

    /// <summary>Einfach-Klick auf einen Import-Eintrag springt zur Original-
    /// Videozeit (exakte Protokollstelle), oeffnet aber KEINEN Edit-Dialog.
    /// User-Wunsch 2026-05-11: "wenn ich mit einem klick im import auf ein
    /// ereignis drücke wird das feld grösser und das video springt auf das
    /// entsprechende punkt. nimm nicht die meter als punkt sondern die
    /// videozeit. dann weiss ich das er genau dort protokoliert hat exakte
    /// stelle."</summary>
    private void ImportEvents_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isImportEditorOpen) return;
        if (e.AddedItems.Count == 0) return;
        if (e.AddedItems[0] is not CodingEvent importEvent) return;
        SeekToImportEvent(importEvent);
    }

    /// <summary>Oeffnet den VsaCodeExplorer-Dialog mit pre-populated Werten
    /// aus dem existierenden Import-Event. Bei OK werden die Aenderungen
    /// direkt in den HaltungRecord.Protocol-Entry zurueckgeschrieben (das
    /// importEvent.Entry IST schon die Original-Referenz).</summary>
    private void OpenImportEditorForExistingEvent(CodingEvent importEvent)
    {
        if (_isImportEditorOpen) return;
        if (_player is null) return;

        _isImportEditorOpen = true;
        try
        {
            _player.SetPause(true);
            SuspendCodingOverlayInput();

            var entry = importEvent.Entry;
            var explorerVm = new ViewModels.Windows.VsaCodeExplorerViewModel(
                entry, entry.MeterStart, entry.Zeit);

            var dlg = new VsaCodeExplorerWindow(explorerVm, _videoPath,
                System.TimeSpan.FromMilliseconds(_player.Time))
            {
                Owner = this,
                LiveSnapshotProvider = () =>
                {
                    var snapPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                        $"import_live_{System.Guid.NewGuid():N}.png");
                    return TakeSnapshotSafe(snapPath) ? snapPath : null;
                }
            };

            var dialogResult = dlg.ShowDialog();

            if (dialogResult != true || dlg.SelectedEntry is null) return;

            var result = dlg.SelectedEntry;
            entry.Code = result.Code;
            entry.Beschreibung = result.Beschreibung;
            entry.CodeMeta = result.CodeMeta;
            entry.MeterStart = result.MeterStart;
            entry.MeterEnd = result.MeterEnd;
            entry.Zeit = result.Zeit;
            entry.IsStreckenschaden = result.IsStreckenschaden;
            entry.FotoPaths = result.FotoPaths;

            importEvent.MeterAtCapture = entry.MeterStart ?? entry.MeterEnd ?? importEvent.MeterAtCapture;
            importEvent.VideoTimestamp = entry.Zeit ?? importEvent.VideoTimestamp;

            // UI-Refresh: CodingEvent hat kein INPC, ListBox erkennt
            // Entry-Aenderungen nicht. Remove+Insert an gleicher Position
            // erzwingt Re-Render.
            var idx = _codingImportEvents.IndexOf(importEvent);
            if (idx >= 0)
            {
                _codingImportEvents.RemoveAt(idx);
                _codingImportEvents.Insert(idx, importEvent);
            }
        }
        finally
        {
            ResumeCodingOverlayInput();
            _isImportEditorOpen = false;
        }
    }

    /// <summary>Context-Menü: Zum Zeitpunkt springen.</summary>
    private void ImportSeek_Click(object sender, RoutedEventArgs e)
    {
        if (LstImportEvents.SelectedItem is not CodingEvent importEvent) return;
        SeekToImportEvent(importEvent);
    }

    /// <summary>Context-Menü: Workflow zum Markieren mit BBox + SAM als
    /// Training-Beispiel fuer einen bestehenden Import-Eintrag.
    /// 2026-05-11 User-Wunsch: Doppelklick fuer Edit, Rechtsklick fuer Training.
    /// Schritt 1 (heute): Spring zum Meter + Markieren-Tool aktivieren +
    /// State setzen, damit der naechste BBox-Save-Pfad den Code aus dem
    /// Original-Entry uebernimmt. Step 2 (separater Slice) verdrahtet die
    /// KB-Speicherung mit BBox + SAM-Maske + Code.</summary>
    private void ImportTrainingWithBbox_Click(object sender, RoutedEventArgs e)
    {
        if (LstImportEvents.SelectedItem is not CodingEvent importEvent) return;
        StartTrainingFromExistingEvent(importEvent, source: "import");
    }

    /// <summary>Context-Menü auf KI-Befund-Liste: gleicher Training-Workflow
    /// fuer einen bestehenden KI-Befund. Aufrufpfad ist identisch zu
    /// ImportTrainingWithBbox_Click — nur die Quell-Liste unterscheidet sich.</summary>
    private void CodingTrainingWithBbox_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent codingEvent) return;
        StartTrainingFromExistingEvent(codingEvent, source: "befund");
    }

    /// <summary>Gemeinsame Helper-Methode: Spring zum Meter, Markieren-Tool
    /// aktivieren, Pending-Training-State setzen. Der eigentliche Save-Pfad
    /// (BBox + SAM + TeacherAnnotation) wird im naechsten Sub-Slice
    /// verdrahtet.</summary>
    private void StartTrainingFromExistingEvent(CodingEvent ev, string source)
    {
        SeekToImportEvent(ev);

        // State fuer Folge-Workflow setzen — Feld wird im Hauptpartial
        // deklariert (PlayerWindow.xaml.cs), Save-Pfad verwendet es.
        _pendingTrainingFromExistingEntry = ev.Entry;

        // User-sichtbarer Hinweis welcher Code uebernommen wird.
        var code = string.IsNullOrWhiteSpace(ev.Entry?.Code) ? "(kein Code)" : ev.Entry!.Code;
        ShowOverlay(
            $"Training-Modus: Code {code} aus {source}. Ziehe BBox um den Bereich, dann mit SAM bestaetigen.",
            System.TimeSpan.FromSeconds(6));

        // Falls Codier-Modus noch nicht aktiv ist: Hinweis statt automatisch
        // aktivieren (Codier-Modus-Aktivierung ist groesserer Lifecycle-
        // Eingriff — separater Slice).
        if (!_isCodingMode)
        {
            ShowOverlay(
                "Bitte erst den Codier-Modus aktivieren, dann das Markieren-Tool waehlen.",
                System.TimeSpan.FromSeconds(4));
        }
    }

    private void SeekToImportEvent(CodingEvent importEvent)
    {
        // 2026-05-11 User-Wunsch: ausschliesslich Video-Zeit verwenden, kein
        // Meter-Fallback — die Original-Protokoll-Stelle soll exakt getroffen
        // werden. Wenn keine Video-Zeit hinterlegt ist: kein Sprung (statt
        // ungenauer Meter-Umrechnung).
        if (_player == null) return;
        if (importEvent.VideoTimestamp.TotalMilliseconds > 0)
            _player.Time = (long)importEvent.VideoTimestamp.TotalMilliseconds;
    }

    /// <summary>
    /// Context-Menü: Import-Eintrag als Training-Sample bestätigen.
    /// Springt zum Zeitpunkt, macht einen Snapshot und erstellt eine Lehrer-Annotation.
    /// </summary>
    private async void ImportConfirm_Click(object sender, RoutedEventArgs e)
    {
        if (LstImportEvents.SelectedItem is not CodingEvent importEvent) return;

        // 1. Zum Zeitpunkt springen
        SeekToImportEvent(importEvent);
        await Task.Delay(200); // Kurz warten bis Frame gerendert ist

        // 2. Frame capturen
        if (!TryTakeSnapshot(out var snapshotPath) || !System.IO.File.Exists(snapshotPath))
        {
            MessageBox.Show("Frame konnte nicht aufgenommen werden.\nBitte prüfen Sie ob das Video läuft.",
                "Import bestätigen", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 3. Bild in teacher_images kopieren
        var imagesDir = AuswertungPro.Next.Application.Ai.Teacher.TeacherAnnotationStore.GetImagesDir();
        var annotationId = Guid.NewGuid().ToString("N")[..12];
        var destFrame = System.IO.Path.Combine(imagesDir, $"mark_{annotationId}.png");
        System.IO.File.Copy(snapshotPath, destFrame, overwrite: true);

        // 4. Lehrer-Annotation erstellen
        var annotation = new AuswertungPro.Next.Application.Ai.Teacher.TeacherAnnotation
        {
            AnnotationId = annotationId,
            VsaCode = importEvent.Entry.Code,
            Beschreibung = importEvent.Entry.Beschreibung,
            MeterPosition = importEvent.MeterAtCapture,
            VideoTimestamp = importEvent.VideoTimestamp,
            ToolType = Domain.Models.OverlayToolType.None,
            FullFramePath = destFrame,
        };

        await AuswertungPro.Next.Application.Ai.Teacher.TeacherAnnotationStore.AppendAsync(annotation);

        // 5. Visuelles Feedback
        try { System.IO.File.Delete(snapshotPath); } catch { }
        OsdMeterBadge.Visibility = Visibility.Visible;
        TxtOsdMeter.Text = $"✓ {importEvent.Entry.Code} @ {importEvent.MeterAtCapture:F1}m bestätigt";
        var resetTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        resetTimer.Tick += (_, _) => { OsdMeterBadge.Visibility = Visibility.Collapsed; resetTimer.Stop(); };
        resetTimer.Start();
    }

    // Phase 6.1.F Sub-C: CodingAcceptDefect_Click + CodingEditDefect_Click +
    // CodingRejectDefect_Click nach PlayerWindow.CodingMode.cs migriert.

    /// <summary>Defekt-Detail-Panel mit Werten des ausgewaehlten Events befuellen.</summary>
    /// Details werden jetzt oben im KI-BEFUNDE Panel angezeigt — unteres Panel bleibt collapsed.
    // Phase 6.1.F Sub-D: UpdateCodingDefectDetailPanel + ColorizeCodingEventListItems + FindCodingChild + UpdateCodingStatistics + ShrinkEnlargedListItem nach PlayerWindow.CodingMode.cs migriert.

    // Phase 6.1.C: CodingStatusToDisplayText nach PlayerWindow.Helpers.cs migriert.


    /// <summary>Rekursiv ein benanntes Kind-Element im VisualTree finden.</summary>

    /// <summary>Statistiken im Seitenpanel aktualisieren (direkt berechnet).</summary>

    // --- Coding: Existierende Protokoll-Eintraege laden ---

    /// <summary>
    /// Laedt existierende Protokoll-Eintraege aus der Haltung (Import/DataGrid) in die Events-Liste.
    /// </summary>
    private void LoadExistingProtocolEntries()
    {
        if (_codingVm == null || _haltungRecord == null) return;

        var entries = _haltungRecord.Protocol?.Current?.Entries?
            .Where(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code))
            .ToList();

        if (entries == null || entries.Count == 0) return;

        foreach (var entry in entries.OrderBy(e => e.MeterStart ?? 0))
        {
            var codingEvent = new CodingEvent
            {
                Entry = entry,
                MeterAtCapture = entry.MeterStart ?? 0,
                VideoTimestamp = entry.Zeit ?? TimeSpan.Zero
            };
            _codingVm.Events.Add(codingEvent);
        }
    }

    // --- Coding: Primaere Schaeden synchronisieren ---

    private void SyncCodingToPrimaryDamages(ProtocolDocument doc)
    {
        if (_haltungRecord == null) return;

        var entries = doc.Current?.Entries?
            .Where(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code))
            .ToList();
        if (entries == null || entries.Count == 0)
        {
            _haltungRecord.SetFieldValue("Primaere_Schaeden", "", FieldSource.Manual, userEdited: true);
            _haltungRecord.ModifiedAtUtc = DateTime.UtcNow;
            return;
        }

        // Zeilen fuer Primaere_Schaeden aufbauen
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = new List<string>();
        foreach (var entry in entries)
        {
            var code = (entry.Code ?? "").Trim();
            if (string.IsNullOrWhiteSpace(code)) continue;

            var meter = entry.MeterStart ?? entry.MeterEnd;
            var meterKey = meter.HasValue ? meter.Value.ToString("F2") : "";
            if (!seen.Add($"{code.ToUpperInvariant()}|{meterKey}")) continue;

            var parts = new List<string>();
            if (meter.HasValue) parts.Add($"{meter.Value:0.00}m");
            parts.Add(code);
            if (!string.IsNullOrWhiteSpace(entry.Beschreibung))
                parts.Add(entry.Beschreibung.Trim().Replace("\r", "").Replace("\n", " "));

            if (entry.CodeMeta?.Parameters != null)
            {
                if (entry.CodeMeta.Parameters.TryGetValue("vsa.q1", out var q1) && !string.IsNullOrWhiteSpace(q1))
                    parts.Add($"Q1={q1}");
                if (entry.CodeMeta.Parameters.TryGetValue("vsa.q2", out var q2) && !string.IsNullOrWhiteSpace(q2))
                    parts.Add($"Q2={q2}");
            }

            lines.Add(string.Join(" ", parts));
        }

        var primaryText = string.Join("\n", lines);
        _haltungRecord.SetFieldValue("Primaere_Schaeden", primaryText, FieldSource.Manual, userEdited: true);
        _haltungRecord.ModifiedAtUtc = DateTime.UtcNow;
    }

    // --- Coding: Protokoll-Vorschau (nachtraeglich bearbeitbar) ---

    private void ShowCodingProtocolPreview(ProtocolDocument doc)
    {
        if (_haltungRecord == null) return;

        var result = MessageBox.Show(
            $"{doc.Current.Entries.Count} Beobachtungen protokolliert.\n\n" +
            "Protokoll jetzt anzeigen und bearbeiten?\n" +
            "(Aenderungen werden in Primaere Schaeden uebernommen)",
            "Codier-Session abgeschlossen",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        var project = ((ViewModels.ShellViewModel?)App.Current.MainWindow?.DataContext)?.Project;
        if (project == null) return;

        var projectFolder = !string.IsNullOrWhiteSpace(App.Resolve<AppSettings>().LastProjectPath)
            ? Path.GetDirectoryName(App.Resolve<AppSettings>().LastProjectPath)
            : null;

        var dlg = new Views.ProtocolObservationsWindow(
            _haltungRecord, project, _videoPath, projectFolder,
            markDirty: () =>
            {
                _haltungRecord.ModifiedAtUtc = DateTime.UtcNow;
            });
        dlg.Owner = this;
        dlg.ShowDialog();

        // Nach Bearbeitung: Primaere Schaeden erneut synchronisieren
        if (_haltungRecord.Protocol != null)
            SyncCodingToPrimaryDamages(_haltungRecord.Protocol);

        // PDF anbieten
        CodingOfferPdfExport(_haltungRecord.Protocol ?? doc);
    }

    // --- Coding: OSD-Timer (liest Meterstand kontinuierlich) ---

    // Phase 6.1.F Sub-A: StartCodingOsdTimer + StopCodingOsdTimer
    // nach PlayerWindow.CodingMode.cs migriert.

    // Phase 6.1.F Sub-F: InitCodingAi + CodingAnalyzeFrame_Click + RunCodingAnalysisAsync nach PlayerWindow.CodingMode.cs migriert.

    /// <summary>Alle Overlays/Einblendungen vom Video entfernen.</summary>
}
