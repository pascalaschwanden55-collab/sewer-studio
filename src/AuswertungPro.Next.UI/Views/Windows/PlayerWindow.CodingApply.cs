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

    // ─── Felder, die LIVE-Features brauchen (MarkTool/OperateurAnnotation/Hotkeys/LiveDetection) ─

    private CodingSessionViewModel? _codingVm;
    private ICodingSessionService? _codingSessionService;
    private IOverlayToolService? _codingOverlayService;
    private readonly SchemaOverlayManager _codingSchemaManager = new();

    // _codingSchemaType / _codingLastOsdMeter werden mit dem aktuellen
    // Schnitt nur gelesen oder genullt — der schreibende Pfad (Schema-
    // Werkzeuge, OSD-Reader) lebte im geloeschten In-Place-Codier-Modus.
    // Pragma unterdrueckt die "nie gesetzt"-Warnung bis ein Folge-Slice die
    // Felder entweder reanimiert oder ganz entfernt.
#pragma warning disable CS0414, CS0649
    // _codingSchemaType wird in MarkTool noch genullt (Reset-Pfad), aber
    // nirgends mehr gelesen — CS0414 unterdrueckt die Warnung, bis ein
    // Folge-Slice das Reset entfernt.
    private SchemaType? _codingSchemaType;

    // Letzter via OSD gelesener Meterstand. Frueher vom OSD-Timer gespeist;
    // wird heute nur noch als Fallback fuer GetMeterFromVideoPosition genutzt
    // (bleibt beim aktuellen Schnitt null, ohne Funktionsverlust).
    private double? _codingLastOsdMeter;
#pragma warning restore CS0414, CS0649

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
}
