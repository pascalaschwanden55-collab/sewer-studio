using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Annotation;

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
    private OperateurAnnotationSession? _operatorSession;
    private CodeTask? _operatorActive;
    private IOperateurAnnotationService? _operatorService;

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
        if (_operatorService is null)
            throw new InvalidOperationException(
                "OperateurAnnotationService nicht gesetzt — App-Bootstrapping muss SetOperateurAnnotationService rufen.");

        _operatorSession = session;
        _operatorActive = null;
        _isOperatorMode = true;

        // Erstes Pending-Task automatisch aktivieren (sofern vorhanden), damit
        // der Operateur direkt einsteigen kann.
        var first = session.FindFirstPending();
        if (first is not null)
            SelectOperatorTask(first);

        try { _player?.SetPause(true); } catch { /* Player ggf. noch nicht geladen */ }
    }

    /// <summary>Beendet den Submodus, verwirft alle ungespeicherten Box/Preview-Daten.</summary>
    public void ExitOperatorMode()
    {
        _isOperatorMode = false;
        _operatorActive = null;
        _operatorSession = null;
    }

    /// <summary>
    /// Setzt das aktive CodeTask. Der Player wird auf die Position des Codes
    /// pausiert; Box+Preview eines vorigen, nicht-committed Tasks fallen
    /// zurueck auf Pending (kein Datenverlust, keine Halb-Annotationen).
    /// Phase 7 ergaenzt das Seek auf den exakten Frame.
    /// </summary>
    public void SelectOperatorTask(CodeTask task)
    {
        if (_operatorSession is null)
            throw new InvalidOperationException("Kein aktiver Operator-Modus.");

        _operatorSession.MoveToCode(task);
        _operatorActive = _operatorSession.Active;

        try { _player?.SetPause(true); } catch { /* best-effort */ }
        // Seek auf Meterstand kommt in Phase 7 (Mapping Meterstand->Frame-Zeit
        // ist keine reine Session-Logik, sondern lebt im Player).
    }

    /// <summary>Operateur uebergeht den aktuellen Code (z.B. Frame unscharf).</summary>
    public void SkipOperatorActive(string reason)
    {
        if (_operatorSession is null) return;
        _operatorSession.MarkActiveSkipped(reason ?? "");

        var next = _operatorSession.FindNextPending();
        if (next is not null)
            SelectOperatorTask(next);
        else
            _operatorActive = null;
    }

    /// <summary>Operateur erkennt Protokollfehler — Code nicht zutreffend.</summary>
    public void RejectOperatorActive(string reason)
    {
        if (_operatorSession is null) return;
        _operatorSession.MarkActiveRejected(reason ?? "");

        var next = _operatorSession.FindNextPending();
        if (next is not null)
            SelectOperatorTask(next);
        else
            _operatorActive = null;
    }

    /// <summary>
    /// Phase 7-Vorlage: ruft Preview + Commit auf dem Service. Phase 6 stellt
    /// den vollstaendigen Aufruf bereit, ohne ihn aus dem UI zu ziehen — die
    /// Box-Erfassung erfolgt erst in Phase 7. Wer hier ruft, gibt die Box
    /// direkt mit (z.B. ein Test oder eine spaetere Box-Drag-Logik).
    /// </summary>
    internal async Task<CommitResult> CommitOperatorActiveAsync(
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

        var request = new AnnotationRequest(
            CaseId: _operatorSession.CaseId,
            Code: _operatorActive.Code,
            ProtocolMeterstand: _operatorActive.Meterstand,
            SuggestedFrameTimeSeconds: actualFrameTimeSeconds,  // Phase 7 setzt das aus dem Mapping
            ActualFrameTimeSeconds: actualFrameTimeSeconds,
            VideoFrameIndex: videoFrameIndex,
            FramePath: framePath,
            FrameWidth: frameWidth,
            FrameHeight: frameHeight,
            Box: box);

        var preview = await _operatorService.PreviewMaskAsync(request, ct).ConfigureAwait(false);
        _operatorActive.Box = box;
        _operatorActive.Preview = preview;
        _operatorActive.State = CodeTaskState.PreviewReady;

        var result = await _operatorService.CommitAsync(request, preview, ct).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            _operatorSession.MarkActiveCommitted(result.SampleId, DateTime.UtcNow);

            var next = _operatorSession.FindNextPending();
            if (next is not null)
                SelectOperatorTask(next);
            else
                _operatorActive = null;
        }
        else
        {
            // Store-Fehler: Task auf Error setzen, Operator entscheidet manuell.
            _operatorActive.State = CodeTaskState.Error;
            _operatorActive.UserReason = result.Error;
        }

        return result;
    }
}
