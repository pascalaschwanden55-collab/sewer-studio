using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Ai.Vision;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AppProtocol = AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.Views.Windows;

// Phase 6.1.G: CodeCatalog-Markieren + Coding-Mode-Apply/Persist + Haltungslaenge
// extrahiert aus PlayerWindow.xaml.cs.
//
// Enthaelt das Oeffnen des Schadenscode-Katalogs aus Markieren-Workflows,
// das Anwenden von Codes (CodingApply_Click), das Persistieren von
// CodingEvents als TrainingSamples und die Fallback-Kette fuer
// Haltungslaenge_m (Field-Resolution + manueller Input).
public partial class PlayerWindow
{
    private void OpenCodeCatalogForMark(string? clockPosition, double timestampSec, string? suggestedCode)
    {
        // Phase 5.1.B Etappe 3.M: via DI-Container.
        AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider? catalog = null;
        try { catalog = App.Resolve<AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider>(); } catch { }

        if (catalog is null)
        {
            _dialogs.ShowMessage(
                "Schadenscode-Katalog nicht verfuegbar.\n" +
                "Bitte die App neu starten oder KI-Einstellungen pruefen.",
                "Markieren", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var entry = new ProtocolEntry
        {
            Source = ProtocolEntrySource.Manual,
            Zeit = TimeSpan.FromSeconds(timestampSec),
        };

        if (!string.IsNullOrWhiteSpace(suggestedCode))
            entry.Code = suggestedCode;

        entry.CodeMeta ??= new ProtocolEntryCodeMeta();
        if (!string.IsNullOrWhiteSpace(clockPosition))
            entry.CodeMeta.Parameters["vsa.uhr.von"] = clockPosition;

        var explorerVm = new ViewModels.Windows.VsaCodeExplorerViewModel(
            entry,
            _codingLastOsdMeter ?? GetMeterFromVideoPosition(),
            TimeSpan.FromSeconds(timestampSec));

        var dlg = new VsaCodeExplorerWindow(explorerVm, _videoPath, TimeSpan.FromSeconds(timestampSec))
        {
            Owner = this
        };

        if (dlg.ShowDialog() == true && dlg.SelectedEntry is not null)
        {
            var result = dlg.SelectedEntry;
            entry.Code = result.Code;
            entry.Beschreibung = result.Beschreibung;
            entry.CodeMeta = result.CodeMeta;
            entry.MeterStart = result.MeterStart;
            entry.MeterEnd = result.MeterEnd;
            entry.Zeit = result.Zeit;
            entry.IsStreckenschaden = result.IsStreckenschaden;
            entry.FotoPaths = result.FotoPaths;

            _onEntryCreated?.Invoke(entry);
            ShowOverlay($"Beobachtung erfasst: {entry.Code}", TimeSpan.FromSeconds(4));
        }
    }

    // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
    // CODIER-MODUS (integriert im PlayerWindow)
    // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

    private bool _isCodingMode;
    private CodingSessionViewModel? _codingVm;
    private ICodingSessionService? _codingSessionService;
    private IOverlayToolService? _codingOverlayService;
    private readonly SchemaOverlayManager _codingSchemaManager = new();
    private SchemaType? _codingSchemaType;

    // Kalibrierung
    private bool _codingIsCalibrating;
    private NormalizedPoint? _codingCalibStart;

    // Overlay-Vorschau
    private System.Windows.Shapes.Line? _codingPreviewLine;

    // Externes Fenster hat Fokus bekommen (nicht eigener Dialog)
    private bool _deactivatedByExternalWindow;

    // Referenz-DN Toggle
    private bool _showReferenceDn;

    // KI Live-Analyse
    private LiveDetectionService? _codingLiveDetection;
    private EnhancedVisionAnalysisService? _codingEnhancedVision;

    /// <summary>
    /// Kandidaten-Tracker: Schaeden die in der Tiefe erkannt wurden, aber noch
    /// nicht bestaetigt sind. Erst wenn die Kamera naeher kommt (Box wird groesser)
    /// wird der Kandidat zum Befund.
    /// Key: YOLO-Klassenname, Value: (erster Frame-Zeitpunkt, Box-Flaeche-Norm, Confidence)
    /// </summary>
    private readonly Dictionary<string, (double TimeSec, double AreaNorm, double Confidence, string Label)>
        _codingDepthCandidates = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _codingAnalysisCts;
    // Slice 8a.3 Step 5a.1: Lese-Zugriff im Kill-Switch entfernt; der
    // Schreibzugriff in ResumeAfterPause bleibt bis 5b dranne. Pragma
    // unterdrueckt CS0414 fuer dieses Uebergangsfenster.
#pragma warning disable CS0414
    private bool _codingIsAnalyzing;
#pragma warning restore CS0414
    private string _codingAiModelName = string.Empty;
    private bool _codingAiPulseRunning;

    // Live-KI Timer (automatische Analyse alle 5s)
    private DispatcherTimer? _codingLiveAiTimer;
    private DispatcherTimer? _codingLiveAiBlinkTimer;
    private bool _codingLiveAiBlinkState;
    private QualityGateService? _codingQualityGate;

    // Eingabemarker-Zustand
    private enum EingabemarkerPhase { Inactive, Drawing, Input, Analyzing }
    private EingabemarkerPhase _eingabemarkerPhase = EingabemarkerPhase.Inactive;
    private Point _eingabemarkerDragStart; // Canvas-Koordinaten
    private Rect _eingabemarkerRectNorm;   // Normiertes Rechteck (0-1)
    private System.Windows.Shapes.Rectangle? _eingabemarkerPreviewRect;

    // Multi-Model Pipeline (YOLO → DINO → SAM) fuer Einzelframe-Analyse
    private AuswertungPro.Next.Infrastructure.Ai.Pipeline.SingleFrameMultiModelService? _codingMultiModel;
    private AuswertungPro.Next.Infrastructure.Ai.Pipeline.VisionPipelineClient? _codingVisionClient;

    // Import-Beobachtungen (Referenz-Spalte, nur-lesen)
    private readonly ObservableCollection<CodingEvent> _codingImportEvents = new();

    // Bestaetigungs-Panel: aktuell wartendes Event
    private CodingEvent? _codingPendingConfirmEvent;
    private QualityGateResult? _codingPendingGateResult;

    // OSD-Meter Timer (liest Meterstand kontinuierlich)
    private DispatcherTimer? _codingOsdTimer;
    private bool _codingOsdReading;
    private int _codingOverlaySuspendDepth;
    private bool _codingOverlayWasOpenBeforeSuspend;

    private void CodingMode_Click(object sender, RoutedEventArgs e)
    {
        if (_haltungRecord == null)
        {
            _dialogs.ShowMessage(
                "Codier-Modus benoetigt eine Haltung.\n" +
                "Bitte das Video ueber die Datenseite mit einer Haltung oeffnen.",
                "Codier-Modus", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_isTrainingMode)
        {
            _dialogs.ShowMessage(
                "Bitte zuerst den Trainings-Modus beenden.",
                "Codier-Modus", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Slice 8a.3 Step 4.5 Bridge: Klick auf "Codier-Modus" oeffnet
        // jetzt CodingModeWindow als Dialog. Der alte In-Place-Pfad
        // (EnterCodingMode + PlayerWindow.Coding*-Partials) bleibt im Code
        // als Fallback — git-revertable wenn der Smoke etwas zeigt. Step 5
        // entfernt ihn endgueltig.
        _player?.SetPause(true);

        var codingWindow = new CodingModeWindow(_haltungRecord, _videoPath)
        {
            Owner = this
        };
        var result = codingWindow.ShowDialog();
        if (result == true && codingWindow.CompletedProtocol != null)
        {
            _haltungRecord.Protocol = codingWindow.CompletedProtocol;
            _haltungRecord.ModifiedAtUtc = DateTime.UtcNow;
            // Slice 8a.3 Step 4.5b: Primaere_Schaeden-Feld aus dem
            // neuen Protokoll synchronisieren — sonst zeigt das DataGrid
            // weiter den alten Stand. SyncCodingToPrimaryDamages lebt
            // in PlayerWindow.ImportProtocol.cs und ist nicht Teil des
            // alten Coding-Mode-Blocks, kann also stehen bleiben wenn
            // Step 5 PlayerWindow.Coding* loescht.
            SyncCodingToPrimaryDamages(codingWindow.CompletedProtocol);
            var entryCount = codingWindow.CompletedProtocol.Current?.Entries?.Count ?? 0;
            ShowOverlay(
                $"Codierung uebernommen ({entryCount} Eintraege)",
                TimeSpan.FromSeconds(3));
        }
    }

    // EnterCodingMode + alle PlayerWindow.Coding*-Partials bleiben als
    // Fallback im Code; werden seit 4.5 nicht mehr von der UI gerufen.
    // Step 5 loescht den ganzen Block, sobald der UI-Smoke gruen ist.

    // Phase 6.1.F Sub-E: EnterCodingMode + LoadExistingProtocolEventsAsImport + ExitCodingMode nach PlayerWindow.CodingMode.cs migriert.



    private void CodingApply_Click(object sender, RoutedEventArgs e)
    {
        if (_codingVm == null || _haltungRecord == null) return;

        // ProtocolDocument aus allen Events aufbauen
        var doc = _haltungRecord.Protocol ?? new ProtocolDocument();
        doc.Current ??= new ProtocolRevision();
        doc.Current.Entries ??= new List<ProtocolEntry>();

        // 1) Aktuelle Coding-Events als "Soll-Zustand" (korrigierte Werte) aufbauen
        var eventEntries = _codingVm.Events
            .Select(ev => ev.Entry)
            .Where(e => e != null && !string.IsNullOrWhiteSpace(e.Code))
            .GroupBy(e => e.EntryId)
            .Select(g => g.Last())
            .ToDictionary(e => e.EntryId, e => e);

        // 2) Vorhandene Protokoll-Eintraege updaten oder als geloescht markieren
        var existingById = doc.Current.Entries.ToDictionary(e => e.EntryId, e => e);
        foreach (var existing in doc.Current.Entries)
        {
            if (eventEntries.TryGetValue(existing.EntryId, out var updated))
            {
                CopyProtocolEntryValues(updated, existing);
                existing.IsDeleted = false;
            }
            else
            {
                existing.IsDeleted = true;
            }
        }

        // 3) Neue Eintraege aus Coding-Events anhaengen
        foreach (var kv in eventEntries)
        {
            if (!existingById.ContainsKey(kv.Key))
                doc.Current.Entries.Add(kv.Value);
        }

        _haltungRecord.Protocol = doc;
        _haltungRecord.ModifiedAtUtc = DateTime.UtcNow;

        // Primaere Schaeden ins DataGrid uebertragen
        SyncCodingToPrimaryDamages(doc);

        // Feedback-Loop: CodingEvents → TrainingSamples persistieren
        // (Im PlayerWindow wird CompleteSession() nicht aufgerufen,
        //  daher muss die Training-Persistierung hier erfolgen.)
        PersistCodingEventsAsTrainingSamples();

        var message = _codingVm.Events.Count == 0
            ? "Primaere Schaeden geleert"
            : $"{_codingVm.Events.Count} Ereignisse in Primaere Schaeden uebernommen";
        ShowOverlay(message, TimeSpan.FromSeconds(4));
    }

    private static void CopyProtocolEntryValues(ProtocolEntry source, ProtocolEntry target)
    {
        target.Code = source.Code;
        target.Beschreibung = source.Beschreibung;
        target.MeterStart = source.MeterStart;
        target.MeterEnd = source.MeterEnd;
        target.IsStreckenschaden = source.IsStreckenschaden;
        target.Mpeg = source.Mpeg;
        target.Zeit = source.Zeit;
        target.Source = source.Source;
        target.CodeMeta = source.CodeMeta;
        target.Ai = source.Ai;
        target.FotoPaths = source.FotoPaths?.ToList() ?? new List<string>();
    }

    /// <summary>
    /// Konvertiert die KI-Events aus dem Codiermodus in TrainingSamples
    /// und speichert sie via TrainingSamplesStore.
    /// Schliesst den Feedback-Loop im PlayerWindow (analog zu CodingSessionService.CompleteSession).
    /// </summary>
    /// <summary>
    /// Speichert ein einzelnes CodingEvent sofort als TrainingSample.
    /// Wird nach jeder Codierung aufgerufen — nicht erst beim Beenden.
    /// </summary>
    private void PersistSingleEventAsTrainingSample(CodingEvent ev)
    {
        if (ev.Entry == null || string.IsNullOrWhiteSpace(ev.Entry.Code)) return;
        try
        {
            var caseId = _codingVm?.HaltungName ?? "unknown";
            var framePath = ev.Entry.FotoPaths.Count > 0 ? ev.Entry.FotoPaths[0] : null;
            var sample = AuswertungPro.Next.Application.Ai.Training.CodingEventToSampleMapper.FromCodingEvent(ev, caseId, framePath);
            if (ev.Entry.FotoPaths.Count > 1)
            {
                sample.AdditionalFramePaths ??= new System.Collections.Generic.List<string>();
                for (int i = 1; i < ev.Entry.FotoPaths.Count; i++)
                    sample.AdditionalFramePaths.Add(ev.Entry.FotoPaths[i]);
            }
            AuswertungPro.Next.Application.Ai.Training.TrainingSamplesStore.MergeAndSaveAsync(new List<AuswertungPro.Next.Domain.Ai.Training.TrainingSample> { sample })
                .SafeFireAndForget("TrainingSaveSingle");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Training] Einzelspeicherung Fehler: {ex.Message}");
        }
    }

    private void PersistCodingEventsAsTrainingSamples()
    {
        if (_codingVm == null || _codingVm.Events.Count == 0) return;
        try
        {
            var caseId = _codingVm.HaltungName ?? "unknown";
            var samples = new System.Collections.Generic.List<AuswertungPro.Next.Domain.Ai.Training.TrainingSample>();
            foreach (var ev in _codingVm.Events)
            {
                var framePath = ev.Entry.FotoPaths.Count > 0 ? ev.Entry.FotoPaths[0] : null;
                var sample = AuswertungPro.Next.Application.Ai.Training.CodingEventToSampleMapper.FromCodingEvent(ev, caseId, framePath);

                // Alle Fotos als zusaetzliche Lernbilder referenzieren
                // (Foto 1 = FramePath, Foto 2+ = AdditionalFrames)
                if (ev.Entry.FotoPaths.Count > 1)
                {
                    sample.AdditionalFramePaths ??= new System.Collections.Generic.List<string>();
                    for (int i = 1; i < ev.Entry.FotoPaths.Count; i++)
                        sample.AdditionalFramePaths.Add(ev.Entry.FotoPaths[i]);
                }

                samples.Add(sample);
            }
            if (samples.Count > 0)
                AuswertungPro.Next.Application.Ai.Training.TrainingSamplesStore.MergeAndSaveAsync(samples)
                    .SafeFireAndForget("TrainingSave");
        }
        catch (Exception ex)
        {
            // Uebernahme darf nie blockiert werden, aber Fehler loggen
            System.Diagnostics.Debug.WriteLine($"[Training] Fehler: {ex.Message}");
        }
    }

    /// <summary>
    /// Stellt sicher, dass Haltungslaenge_m gesetzt ist.
    /// Fallback-Kette: Haltungslaenge_m → Laenge_m → DamageOverlay → Protokoll BCE → manuelle Eingabe.
    /// </summary>
    private void EnsureHaltungslaenge(HaltungRecord record)
    {
        // Bereits vorhanden?
        if (HasValidLength(record, "Haltungslaenge_m"))
            return;

        // Fallback 1: Laenge_m
        if (HasValidLength(record, "Laenge_m"))
        {
            record.SetFieldValue("Haltungslaenge_m",
                record.GetFieldValue("Laenge_m"),
                Domain.Models.FieldSource.Legacy, userEdited: false);
            return;
        }

        // Fallback 2: DamageOverlay (wurde beim Oeffnen aus dem Protokoll berechnet)
        if (_damageOverlay != null && _damageOverlay.PipeLengthMeters > 0)
        {
            record.SetFieldValue("Haltungslaenge_m",
                _damageOverlay.PipeLengthMeters.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                Domain.Models.FieldSource.Legacy, userEdited: false);
            return;
        }

        // Fallback 3: Protokoll BCE-Eintrag (Rohrende) → hoechster Meter
        if (record.Protocol?.Current?.Entries is { Count: > 0 } entries)
        {
            var maxMeter = entries
                .Where(e => e.MeterStart.HasValue && e.MeterStart.Value > 0)
                .Select(e => e.MeterStart!.Value)
                .DefaultIfEmpty(0)
                .Max();

            if (maxMeter > 0)
            {
                record.SetFieldValue("Haltungslaenge_m",
                    maxMeter.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    Domain.Models.FieldSource.Legacy, userEdited: false);
                return;
            }
        }

        // Fallback 4: Benutzer manuell fragen
        var input = Microsoft.VisualBasic.Interaction.InputBox(
            "Haltungslaenge konnte nicht ermittelt werden.\n" +
            "Bitte Haltungslaenge in Meter eingeben (z.B. 45.3):",
            "Haltungslaenge eingeben", "");

        if (!string.IsNullOrWhiteSpace(input))
        {
            var normalized = input.Trim().Replace(',', '.');
            if (double.TryParse(normalized, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var val) && val > 0)
            {
                record.SetFieldValue("Haltungslaenge_m",
                    val.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    Domain.Models.FieldSource.Manual, userEdited: true);
            }
        }
    }

    // Phase 6.1.C: HasValidLength nach PlayerWindow.Helpers.cs migriert.
}
