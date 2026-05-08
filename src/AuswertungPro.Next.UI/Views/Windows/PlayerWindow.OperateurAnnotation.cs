using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Application.Ai.Annotation;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

// Slice 1 (Operateur-Annotation): Sub-Modus innerhalb Trainings-Modus.
// Operateur importiert Haltungsordner (Video + PDF), bekommt eine Liste der
// VSA-Codes aus dem Protokoll, klickt einen Code, zieht eine Box, SAM
// segmentiert, Operateur bestaetigt -> Sample landet in Store + KB + YOLO.
//
// Bewusst KEIN ViewModel — bestehendes PlayerWindow-Pattern: direkte Felder
// im Partial plus XAML-Bindings gegen x:Name-Elemente (siehe MarkTool.cs,
// TrainingMode.cs). Plan-Header B5: ein PlayerWindow-VM existiert nicht.
//
// Phase 6 ist Skeleton + Logik. Box-Drag, SAM-Preview-Render, XAML-Panel
// und Hotkey-Bindings folgen in Phase 7. Bis dahin sind die Methoden
// hier ueber Code aufrufbar (z.B. aus Tests oder einem zukuenftigen
// Import-Handler) — Status-Maschinerie und Persistierungs-Vertrag sind
// schon einsatzbereit.
public partial class PlayerWindow
{
    private bool _isOperatorMode;
    private bool _operatorBoxActive;        // Phase 7: Box-Tool aktiv?
    private OperateurAnnotationSession? _operatorSession;
    private CodeTask? _operatorActive;
    private IOperateurAnnotationService? _operatorService;

    // PreviewReady-Zwischenzustand: SAM-Maske ist da, aber noch nicht
    // committed. Bleibt zwischen Box-Drawing-Complete und Confirm/Discard
    // erhalten, damit der Operator die Maske ansehen und entscheiden kann.
    private MaskPreview? _operatorPendingPreview;
    private string? _operatorPendingFramePath;
    private int _operatorPendingFrameWidth;
    private int _operatorPendingFrameHeight;
    private double _operatorPendingFrameTime;
    private BoundingBoxNormalized? _operatorPendingBox;

    /// <summary>
    /// Aktiver Submodus? Wird in Phase 7 fuer Sichtbarkeit/HitTest gegen den
    /// Player-Overlay-Canvas gebraucht; bereits jetzt verfuegbar fuer Tests.
    /// </summary>
    internal bool IsOperateurAnnotationModeActive => _isOperatorMode;

    /// <summary>
    /// Wird vom App-Bootstrapping einmal aufgerufen, sobald der DI-Container
    /// den Service kennt (Phase 9: ServiceCollectionConfigurator). Vor dem
    /// ersten EnterOperatorMode muss das gesetzt sein.
    /// </summary>
    public void SetOperateurAnnotationService(IOperateurAnnotationService service)
    {
        _operatorService = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <summary>
    /// Aktiviert den Operateur-Annotation-Submodus mit einer fertig
    /// aufgebauten Session (CaseId + Codes aus dem Protokoll). Der Aufrufer
    /// hat das PDF schon geparst und die Code-Liste gefuellt — der Service
    /// haelt die Liste nicht selbst.
    /// </summary>
    public void EnterOperatorMode(OperateurAnnotationSession session)
    {
        if (session is null) throw new ArgumentNullException(nameof(session));

        // Service kann via SetOperateurAnnotationService injiziert worden
        // sein (z.B. fuer Tests) ODER lazy aus dem Accessor gezogen werden,
        // den das App-Bootstrapping nach DI-Build befuellt. Beides erlaubt.
        _operatorService ??= OperateurAnnotationServiceAccessor.Current;
        if (_operatorService is null)
            throw new InvalidOperationException(
                "OperateurAnnotationService nicht verfuegbar — KI-Pipeline aus oder App-Bootstrapping unvollstaendig.");

        _operatorSession = session;
        _operatorActive = null;
        _isOperatorMode = true;

        // UI: OperatorPanel sichtbar, TrainingPanel raus. Code-Liste direkt
        // an Tasks binden (B5: kein VM, ItemsSource per Code-Behind).
        if (OperatorSidePanel != null)
            OperatorSidePanel.Visibility = Visibility.Visible;
        if (TrainingSidePanel != null)
            TrainingSidePanel.Visibility = Visibility.Collapsed;
        if (TrainingSidePanelColumn != null)
            TrainingSidePanelColumn.Width = new GridLength(280);

        if (OperatorCodeList != null)
            OperatorCodeList.ItemsSource = session.Tasks;
        UpdateOperatorStatusUi();

        // Erstes Pending-Task automatisch aktivieren (sofern vorhanden), damit
        // der Operateur direkt einsteigen kann.
        var first = session.FindFirstPending();
        if (first is not null)
            SelectOperatorTask(first);

        try { _player?.SetPause(true); } catch { /* Player ggf. noch nicht geladen */ }
    }

    /// <summary>
    /// Beendet den Submodus, verwirft alle ungespeicherten Box/Preview-Daten,
    /// schliesst das Box-Overlay und stellt die normale Trainings-Sicht wieder
    /// her. Der Operator landet in dem Zustand, aus dem er den Submodus
    /// gestartet hat (Trainings-Modus aktiv, Side-Panel sichtbar) — sonst
    /// wuerde die rechte Spalte leer wirken oder ein Overlay-Popup haengen.
    /// </summary>
    public void ExitOperatorMode()
    {
        // Erst Overlay-Pipeline abschalten, BEVOR die Felder gecleart werden —
        // sonst waere _operatorBoxActive evtl. noch true, und ein nachhinkender
        // HandleMarkDrawingComplete-Tick wuerde HandleOperatorBoxCompleteAsync
        // mit halben Daten triggern.
        DeactivateOperatorBoxTool();

        _isOperatorMode = false;
        _operatorActive = null;
        _operatorSession = null;

        // OperatorPanel raus, TrainingPanel zurueck — der Operator bleibt im
        // Trainings-Modus, das war der Einstiegspunkt.
        if (OperatorSidePanel != null)
            OperatorSidePanel.Visibility = Visibility.Collapsed;
        if (TrainingSidePanel != null && _isTrainingMode)
            TrainingSidePanel.Visibility = Visibility.Visible;
        if (OperatorCodeList != null)
            OperatorCodeList.ItemsSource = null;
        if (BtnOperatorBox != null) BtnOperatorBox.IsEnabled = false;
        if (BtnOperatorSkip != null) BtnOperatorSkip.IsEnabled = false;
        if (BtnOperatorReject != null) BtnOperatorReject.IsEnabled = false;
        if (TxtOperatorStatus != null)
            TxtOperatorStatus.Text = "Keine Sitzung — Haltungsordner importieren.";
        if (TxtOperatorProgress != null) TxtOperatorProgress.Text = "";
    }

    /// <summary>
    /// Aktualisiert Status-Text + Progress-Text basierend auf der Session.
    /// Nur Code-Behind-Schreiben gegen die x:Name-Elemente — kein Binding.
    /// CodeTask hat kein INotifyPropertyChanged (lebt in Application-Schicht
    /// ohne UI-Dependency), darum muss die ListBox nach State-Wechseln ihre
    /// Status-Dots neu zeichnen — Items.Refresh() ist der pragmatische Weg.
    /// </summary>
    private void UpdateOperatorStatusUi()
    {
        if (OperatorCodeList?.Items is { } items)
        {
            try { items.Refresh(); }
            catch { /* Refresh ist best-effort */ }
        }
        if (_operatorSession is null)
        {
            if (TxtOperatorStatus != null)
                TxtOperatorStatus.Text = "Keine Sitzung — Haltungsordner importieren.";
            if (TxtOperatorProgress != null) TxtOperatorProgress.Text = "";
            return;
        }

        var total = _operatorSession.Tasks.Count;
        var done = 0;
        foreach (var t in _operatorSession.Tasks)
            if (OperateurAnnotationSession.IsTerminal(t.State)) done++;

        if (TxtOperatorProgress != null)
            TxtOperatorProgress.Text = $"{done}/{total} Codes erledigt";

        if (TxtOperatorStatus != null)
        {
            if (_operatorActive is null)
                TxtOperatorStatus.Text = total == done && total > 0
                    ? "Alle Codes erledigt."
                    : "Code aus Liste waehlen.";
            else
                TxtOperatorStatus.Text =
                    $"Aktiv: {_operatorActive.Code} @ {_operatorActive.Meterstand:F2} m";
        }

        var hasActiveNonTerminal = _operatorActive is not null
            && !OperateurAnnotationSession.IsTerminal(_operatorActive.State);
        var hasPendingPreview = _operatorPendingPreview is not null
            && _operatorActive is not null
            && _operatorActive.State == CodeTaskState.PreviewReady;

        if (BtnOperatorBox != null)
        {
            BtnOperatorBox.IsEnabled = hasActiveNonTerminal;
            // Wenn schon eine Maske wartet: Button-Text wechselt zu
            // "Erneut zeichnen" — drueckt klar aus, dass die alte Preview
            // verworfen wird.
            BtnOperatorBox.Content = hasPendingPreview
                ? "↻ Erneut zeichnen"
                : "▢ Box ziehen + SAM";
        }
        if (BtnOperatorConfirm != null)
            BtnOperatorConfirm.IsEnabled = hasPendingPreview;
        if (BtnOperatorSkip != null) BtnOperatorSkip.IsEnabled = hasActiveNonTerminal;
        if (BtnOperatorReject != null) BtnOperatorReject.IsEnabled = hasActiveNonTerminal;
    }

    /// <summary>
    /// Setzt das aktive CodeTask. Der Player wird auf die Position des Codes
    /// pausiert; Box+Preview eines vorigen, nicht-committed Tasks fallen
    /// zurueck auf Pending (kein Datenverlust, keine Halb-Annotationen).
    /// Mapping Meterstand-&gt;Frame-Zeit ist Sache des Importers (Slice 1
    /// haelt die Suggested-Time im Request fest, kein automatisches Seek).
    /// </summary>
    public void SelectOperatorTask(CodeTask task)
    {
        if (_operatorSession is null)
            throw new InvalidOperationException("Kein aktiver Operator-Modus.");

        // Wechsel auf anderes Task -> evtl. anhaengende Preview verwerfen.
        if (!ReferenceEquals(task, _operatorActive))
            DiscardOperatorPreview();

        _operatorSession.MoveToCode(task);
        _operatorActive = _operatorSession.Active;

        try { _player?.SetPause(true); } catch { /* best-effort */ }

        // Selektion in der ListBox spiegeln, falls die Auswahl programmatisch
        // (z.B. aus EnterOperatorMode oder nach Commit) angestossen wurde.
        if (OperatorCodeList != null && !ReferenceEquals(OperatorCodeList.SelectedItem, task))
            OperatorCodeList.SelectedItem = task;

        UpdateOperatorStatusUi();
    }

    /// <summary>
    /// Operateur uebergeht den aktuellen Code (z.B. Frame unscharf).
    /// No-op wenn kein Active oder Active bereits in einem terminalen State —
    /// schuetzt vor Doppel-Klick und stale Hotkeys auf bereits abgehakten Tasks.
    /// </summary>
    public void SkipOperatorActive(string reason)
    {
        if (_operatorSession?.Active is null) return;
        if (OperateurAnnotationSession.IsTerminal(_operatorSession.Active.State)) return;

        DiscardOperatorPreview();
        _operatorSession.MarkActiveSkipped(reason ?? "");

        var next = _operatorSession.FindNextPending();
        if (next is not null)
        {
            SelectOperatorTask(next);
        }
        else
        {
            _operatorActive = null;
            UpdateOperatorStatusUi();
        }
    }

    /// <summary>
    /// Operateur erkennt Protokollfehler — Code nicht zutreffend.
    /// Gleiche Defensiv-Logik wie <see cref="SkipOperatorActive"/>.
    /// </summary>
    public void RejectOperatorActive(string reason)
    {
        if (_operatorSession?.Active is null) return;
        if (OperateurAnnotationSession.IsTerminal(_operatorSession.Active.State)) return;

        DiscardOperatorPreview();
        _operatorSession.MarkActiveRejected(reason ?? "");

        var next = _operatorSession.FindNextPending();
        if (next is not null)
        {
            SelectOperatorTask(next);
        }
        else
        {
            _operatorActive = null;
            UpdateOperatorStatusUi();
        }
    }

    // ── XAML-Click-Handler (von OperatorSidePanel verdrahtet) ────────────

    private void OperatorCodeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_operatorSession is null) return;
        if (OperatorCodeList?.SelectedItem is not CodeTask selected) return;
        // Vermeide Loops, falls SelectOperatorTask die Selektion programmatisch
        // gerade umsetzt.
        if (ReferenceEquals(selected, _operatorActive)) return;
        SelectOperatorTask(selected);
    }

    private void OperatorSkip_Click(object sender, RoutedEventArgs e)
        => SkipOperatorActive("Skip-Button");

    private void OperatorReject_Click(object sender, RoutedEventArgs e)
        => RejectOperatorActive("Reject-Button");

    private void OperatorExit_Click(object sender, RoutedEventArgs e)
        => ExitOperatorMode();

    private void OperatorBox_Click(object sender, RoutedEventArgs e)
    {
        // "Erneut zeichnen": pending Preview verwerfen, dann Box-Tool aktivieren.
        DiscardOperatorPreview();
        UpdateOperatorStatusUi();
        ActivateOperatorBoxTool();
    }

    private async void OperatorConfirm_Click(object sender, RoutedEventArgs e)
    {
        if (_isWindowClosed) return;
        try
        {
            await ConfirmOperatorPreviewAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            if (TxtOperatorStatus != null)
                TxtOperatorStatus.Text = $"Fehler: {ex.Message}";
        }
    }

    /// <summary>
    /// Aktiviert das Rectangle-Tool im Operator-Pfad. Reuse vom bestehenden
    /// Mark-Tool-Pattern (B6: WPF-Airspace mit LibVLC) — gleiche
    /// CodingOverlayPopup/Canvas-Pipeline wie ActivateMarkTool, nur mit
    /// einem Operator-Routing-Flag.
    /// </summary>
    private void ActivateOperatorBoxTool()
    {
        if (_operatorActive is null) return;
        if (OperateurAnnotationSession.IsTerminal(_operatorActive.State)) return;

        try { _player?.SetPause(true); } catch { /* best-effort */ }

        EnsureMarkOverlayReady();
        if (_codingOverlayService is null) return;

        _operatorBoxActive = true;
        _codingOverlayService.ActiveTool = OverlayToolType.Rectangle;
        if (_codingVm != null) _codingVm.CurrentOverlay = null;

        if (CodingOverlayPopup != null)
        {
            CodingOverlayPopup.IsOpen = true;
            UpdateCodingOverlayViewport();
        }
        if (CodingOverlayCanvas != null)
        {
            CodingOverlayCanvas.IsHitTestVisible = true;
            CodingOverlayCanvas.Cursor = Cursors.Cross;
        }

        if (TxtOperatorStatus != null)
            TxtOperatorStatus.Text = $"Box ueber {_operatorActive.Code} ziehen…";
    }

    /// <summary>Schliesst das Box-Overlay und setzt das Operator-Flag zurueck.</summary>
    private void DeactivateOperatorBoxTool()
    {
        _operatorBoxActive = false;
        if (_codingOverlayService is not null)
        {
            _codingOverlayService.CancelDraw();
            _codingOverlayService.ActiveTool = OverlayToolType.None;
        }
        if (_codingVm is not null) _codingVm.CurrentOverlay = null;
        if (CodingOverlayPopup != null) CodingOverlayPopup.IsOpen = false;
        if (CodingOverlayCanvas != null)
        {
            CodingOverlayCanvas.IsHitTestVisible = false;
            CodingOverlayCanvas.Cursor = Cursors.Arrow;
        }
    }

    /// <summary>
    /// Wird aus <see cref="HandleMarkDrawingComplete"/> early-branched gerufen,
    /// wenn der Operator-Submodus eine Box gezeichnet hat. Snapshot + BBox
    /// aus der Overlay-Geometrie ableiten, Service.PreviewMaskAsync rufen,
    /// UI aktualisieren — KEIN automatisches Commit. Operator bestaetigt
    /// per Strg+Enter / "Bestaetigen"-Button.
    /// </summary>
    private async Task HandleOperatorBoxCompleteAsync()
    {
        if (_isWindowClosed) return;
        if (_operatorSession is null || _operatorActive is null)
        {
            DeactivateOperatorBoxTool();
            return;
        }

        var overlay = _codingVm?.CurrentOverlay;
        if (overlay is null || overlay.Points is null || overlay.Points.Count < 2)
        {
            DeactivateOperatorBoxTool();
            return;
        }

        if (TxtOperatorStatus != null)
            TxtOperatorStatus.Text = "SAM segmentiert …";

        var actualSec = (_player?.Time ?? 0) / 1000.0;

        // Frame in *stabile* Temp-Datei. Wir behalten die Datei bis zum
        // Confirm/Discard, damit der Operator die Maske ansehen kann ohne
        // dass der Service ihn schon ins KI_BRAIN gemoved hat.
        var tmpPath = Path.Combine(Path.GetTempPath(),
            $"sewerstudio_operator_{Guid.NewGuid():N}.png");

        try
        {
            TakeSnapshotSafe(tmpPath);
            for (var i = 0; i < 30; i++)
            {
                await Task.Delay(50);
                if (File.Exists(tmpPath) && new FileInfo(tmpPath).Length > 100) break;
            }
            if (!File.Exists(tmpPath))
            {
                if (TxtOperatorStatus != null)
                    TxtOperatorStatus.Text = "Frame-Capture leer — Video pausiert?";
                DeactivateOperatorBoxTool();
                return;
            }

            var (frameW, frameH) = ReadPngDimensions(tmpPath);
            var box = BuildBoxFromOverlay(overlay);

            try
            {
                await PreviewOperatorActiveAsync(
                    box: box,
                    videoFrameIndex: 0,
                    actualFrameTimeSeconds: actualSec,
                    frameWidth: frameW,
                    frameHeight: frameH,
                    framePath: tmpPath,
                    ct: CancellationToken.None);

                if (TxtOperatorStatus != null)
                    TxtOperatorStatus.Text =
                        "Maske bereit — Strg+Enter zum Bestaetigen, Box-Button neu zeichnen.";
                UpdateOperatorStatusUi();
            }
            catch (Exception ex)
            {
                if (TxtOperatorStatus != null)
                    TxtOperatorStatus.Text = $"Fehler: {ex.Message}";
                // Preview fehlgeschlagen -> temp-File loeschen, kein Pending behalten.
                try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { }
            }
        }
        finally
        {
            // Box-Tool ausschalten; tmpPath bleibt liegen, falls Preview
            // erfolgreich war — ClearPendingPreviewFields raeumt es spaeter ab.
            DeactivateOperatorBoxTool();
        }
    }

    private static BoundingBoxNormalized BuildBoxFromOverlay(OverlayGeometry overlay)
    {
        var minX = overlay.Points.Min(p => p.X);
        var maxX = overlay.Points.Max(p => p.X);
        var minY = overlay.Points.Min(p => p.Y);
        var maxY = overlay.Points.Max(p => p.Y);
        return new BoundingBoxNormalized(
            XCenter: (minX + maxX) / 2.0,
            YCenter: (minY + maxY) / 2.0,
            Width: Math.Max(maxX - minX, 0.001),
            Height: Math.Max(maxY - minY, 0.001));
    }

    private static (int Width, int Height) ReadPngDimensions(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        var decoder = new PngBitmapDecoder(stream,
            BitmapCreateOptions.None, BitmapCacheOption.Default);
        var frame = decoder.Frames[0];
        return (frame.PixelWidth, frame.PixelHeight);
    }

    // ── Hotkeys (wird aus PlayerWindow_PreviewKeyDown geroutet) ──────────

    /// <summary>
    /// Operator-spezifische Hotkeys. Liefert <c>true</c>, wenn die Taste
    /// behandelt wurde — der Aufrufer setzt dann <c>e.Handled = true</c>.
    /// Wird nur gerufen, wenn <see cref="_isOperatorMode"/> aktiv ist.
    /// </summary>
    private bool Operator_TryHandleKey(KeyEventArgs e)
    {
        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

        // ESC: Operator-Modus verlassen (vor dem Trainings-ESC-Notausstieg).
        if (e.Key == Key.Escape)
        {
            ExitOperatorMode();
            return true;
        }

        // Strg+Eingabe: gewartete SAM-Maske bestaetigen.
        if (ctrl && !shift && (e.Key == Key.Enter || e.Key == Key.Return))
        {
            // async-void via fire-and-forget Helper, damit der Hotkey-Handler
            // sofort zurueckkehrt. Fehler werden in den Status-Text geschrieben.
            _ = TryConfirmFromHotkeyAsync();
            return true;
        }

        // Strg+Umsch+Z: aktuellen Code ueberspringen.
        if (ctrl && shift && e.Key == Key.Z)
        {
            SkipOperatorActive("Hotkey:Ctrl+Shift+Z");
            return true;
        }

        // Strg+R: Code als nicht zutreffend markieren.
        if (ctrl && !shift && e.Key == Key.R)
        {
            RejectOperatorActive("Hotkey:Ctrl+R");
            return true;
        }

        return false;
    }

    private async Task TryConfirmFromHotkeyAsync()
    {
        if (_isWindowClosed) return;
        try { await ConfirmOperatorPreviewAsync(CancellationToken.None); }
        catch (Exception ex)
        {
            if (TxtOperatorStatus != null)
                TxtOperatorStatus.Text = $"Fehler: {ex.Message}";
        }
    }

    /// <summary>
    /// Phase 1 des Two-Phase-API: ruft das Sidecar mit return_polygon=true
    /// und legt die Maske im Pending-Slot ab. Persistiert NICHTS — der
    /// Operator sieht die Maske, kann verwerfen und neu zeichnen, oder per
    /// Confirm festschreiben (Strg+Enter / Bestaetigen-Button).
    /// </summary>
    internal async Task PreviewOperatorActiveAsync(
        BoundingBoxNormalized box,
        int videoFrameIndex,
        double actualFrameTimeSeconds,
        int frameWidth,
        int frameHeight,
        string framePath,
        CancellationToken ct)
    {
        if (_operatorService is null)
            throw new InvalidOperationException(
                "OperateurAnnotationService nicht gesetzt — App-Bootstrapping muss SetOperateurAnnotationService rufen.");
        if (_operatorSession is null || _operatorActive is null)
            throw new InvalidOperationException("Kein aktives CodeTask.");
        if (OperateurAnnotationSession.IsTerminal(_operatorActive.State))
            throw new InvalidOperationException(
                $"CodeTask ist bereits {_operatorActive.State} — terminaler State darf nicht ueberschrieben werden.");

        // Etwaige alte Preview verwerfen — der Operator zieht eine NEUE Box.
        DiscardOperatorPreview();

        var request = new AnnotationRequest(
            CaseId: _operatorSession.CaseId,
            Code: _operatorActive.Code,
            ProtocolMeterstand: _operatorActive.Meterstand,
            SuggestedFrameTimeSeconds: actualFrameTimeSeconds,
            ActualFrameTimeSeconds: actualFrameTimeSeconds,
            VideoFrameIndex: videoFrameIndex,
            FramePath: framePath,
            FrameWidth: frameWidth,
            FrameHeight: frameHeight,
            Box: box);

        // KEIN ConfigureAwait(false) — Continuation mutiert UI-Felder + State.
        var preview = await _operatorService.PreviewMaskAsync(request, ct);

        _operatorActive.Box = box;
        _operatorActive.Preview = preview;
        _operatorActive.State = CodeTaskState.PreviewReady;

        _operatorPendingPreview = preview;
        _operatorPendingFramePath = framePath;
        _operatorPendingFrameWidth = frameWidth;
        _operatorPendingFrameHeight = frameHeight;
        _operatorPendingFrameTime = actualFrameTimeSeconds;
        _operatorPendingBox = box;
    }

    /// <summary>
    /// Phase 2 des Two-Phase-API: schreibt das Sample (Store > YOLO > KB).
    /// Verlangt einen vorigen <see cref="PreviewOperatorActiveAsync"/>-Aufruf;
    /// no-op wenn keine Pending-Preview da ist.
    /// </summary>
    internal async Task<CommitResult?> ConfirmOperatorPreviewAsync(CancellationToken ct)
    {
        if (_operatorService is null)
            throw new InvalidOperationException(
                "OperateurAnnotationService nicht gesetzt.");
        if (_operatorSession is null || _operatorActive is null) return null;
        if (_operatorPendingPreview is null
            || _operatorPendingBox is null
            || _operatorPendingFramePath is null) return null;
        if (_operatorActive.State != CodeTaskState.PreviewReady) return null;

        var request = new AnnotationRequest(
            CaseId: _operatorSession.CaseId,
            Code: _operatorActive.Code,
            ProtocolMeterstand: _operatorActive.Meterstand,
            SuggestedFrameTimeSeconds: _operatorPendingFrameTime,
            ActualFrameTimeSeconds: _operatorPendingFrameTime,
            VideoFrameIndex: 0,
            FramePath: _operatorPendingFramePath,
            FrameWidth: _operatorPendingFrameWidth,
            FrameHeight: _operatorPendingFrameHeight,
            Box: _operatorPendingBox);

        var result = await _operatorService.CommitAsync(request, _operatorPendingPreview, ct);

        if (result.IsSuccess)
        {
            _operatorSession.MarkActiveCommitted(result.SampleId, DateTime.UtcNow);
            ClearPendingPreviewFields(deleteTempFile: true);

            var next = _operatorSession.FindNextPending();
            if (next is not null)
                SelectOperatorTask(next);
            else
            {
                _operatorActive = null;
                UpdateOperatorStatusUi();
            }
        }
        else
        {
            // Store-Fehler: Task auf Error, pending bleibt — Operator kann
            // erneut Confirm versuchen (z.B. nach Sidecar-Reconnect) oder
            // verwerfen.
            _operatorActive.State = CodeTaskState.Error;
            _operatorActive.UserReason = result.Error;
            UpdateOperatorStatusUi();
        }

        return result;
    }

    /// <summary>
    /// Verwirft eine wartende Preview (Maske + Box + temp-Frame). Setzt das
    /// aktive Task von PreviewReady zurueck auf Active, damit der Operator
    /// neu zeichnen kann. Loescht die temp-Frame-Datei (best-effort).
    /// </summary>
    internal void DiscardOperatorPreview()
    {
        ClearPendingPreviewFields(deleteTempFile: true);

        if (_operatorActive is not null
            && _operatorActive.State == CodeTaskState.PreviewReady)
        {
            _operatorActive.State = CodeTaskState.Active;
            _operatorActive.Box = null;
            _operatorActive.Preview = null;
        }
    }

    private void ClearPendingPreviewFields(bool deleteTempFile)
    {
        if (deleteTempFile
            && _operatorPendingFramePath is not null
            && File.Exists(_operatorPendingFramePath))
        {
            try { File.Delete(_operatorPendingFramePath); } catch { /* best-effort */ }
        }

        _operatorPendingPreview = null;
        _operatorPendingFramePath = null;
        _operatorPendingBox = null;
        _operatorPendingFrameWidth = 0;
        _operatorPendingFrameHeight = 0;
        _operatorPendingFrameTime = 0;
    }
}
