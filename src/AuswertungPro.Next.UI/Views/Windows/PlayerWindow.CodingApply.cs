using System;
using System.Linq;
using System.Windows;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

// Phase 6.1.G: CodeCatalog-Markieren + Bridge zu CodingModeWindow.
//
// Slice 8a.3 Step 5b: Der gesamte In-Place-Coding-Modus (alle PlayerWindow.Coding*-
// Partials) ist geloescht. Was hier bleibt, ist:
//   1. OpenCodeCatalogForMark — Live-Markieren-Workflow ruft den VsaCodeExplorer
//      direkt aus dem PlayerWindow auf (kein Coding-Modus noetig).
//   2. CodingMode_Click — Bridge: oeffnet das eigenstaendige CodingModeWindow
//      als Dialog, schreibt das Resultat zurueck.
//   3. EnsureHaltungslaenge — Fallback-Kette fuer das Haltungslaenge_m-Feld;
//      bleibt im PlayerWindow weil sie vom Live-Workflow gerufen wird.
//   4. Felder, die LIVE-Features (MarkTool, OperateurAnnotation, Hotkeys,
//      LiveDetection) noch brauchen: _codingVm, _codingSessionService,
//      _codingOverlayService, _codingSchemaManager, _codingSchemaType,
//      _codingVisionClient, _codingLastOsdMeter.
public partial class PlayerWindow
{
    private void OpenCodeCatalogForMark(string? clockPosition, double timestampSec, string? suggestedCode)
    {
        // Phase 5.1.B Etappe 3.M: via DI-Container.
        AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider? catalog = null;
        try { catalog = App.Resolve<AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider>(); } catch (Exception _bestEffortEx) { System.Diagnostics.Debug.WriteLine($"[best-effort] {_bestEffortEx.Message}"); }

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
            GetMeterFromVideoPosition(),
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

    // ─── Felder, die LIVE-Features brauchen (MarkTool/OperateurAnnotation/Hotkeys/LiveDetection) ─
    //
    // Slice 8a PlayerWindow-Cleanup (2026-05-10): die nach 5b residualen
    // pragma-suppressed Felder _codingSchemaType (nur Reset, nie gelesen)
    // und _codingLastOsdMeter (nie gesetzt, immer null) wurden entfernt.
    // Aufrufer von _codingLastOsdMeter (OpenCodeCatalogForMark) nutzt
    // jetzt direkt GetMeterFromVideoPosition().

    private CodingSessionViewModel? _codingVm;
    private ICodingSessionService? _codingSessionService;
    private IOverlayToolService? _codingOverlayService;
    private readonly SchemaOverlayManager _codingSchemaManager = new();

    // Sidecar-Client fuer SAM-Preview im MarkTool. Slice 8a.3 Step 5b-Fix:
    // wird beim ersten Bedarf in PlayerWindow.MarkTool.TryEnsureCodingVisionClient
    // initialisiert (PipelineConfigProvider liefert die URL).
    private VisionPipelineClient? _codingVisionClient;

    // ─── Bridge: Codier-Modus-Klick oeffnet CodingModeWindow ─────────────────

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

        // Slice 8a.3 Step 4.5 Bridge: Klick auf "Codier-Modus" oeffnet das
        // CodingModeWindow als Dialog. Der alte In-Place-Pfad wurde in Step 5b
        // entfernt — diese Bridge ist jetzt der einzige Eintritt in den
        // Codier-Workflow.
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
            // in PlayerWindow.ImportProtocol.cs.
            SyncCodingToPrimaryDamages(codingWindow.CompletedProtocol);
            var entryCount = codingWindow.CompletedProtocol.Current?.Entries?.Count ?? 0;
            ShowOverlay(
                $"Codierung uebernommen ({entryCount} Eintraege)",
                TimeSpan.FromSeconds(3));
        }
    }

    // EnsureHaltungslaenge wurde im Slice 8a PlayerWindow-Cleanup
    // (2026-05-10) entfernt — der einzige Caller war der in 5b geloeschte
    // In-Place-Coding-Mode. Falls eine Folge-Slice die Fallback-Kette
    // wieder braucht, siehe git-history vor Commit 5187449.
}
