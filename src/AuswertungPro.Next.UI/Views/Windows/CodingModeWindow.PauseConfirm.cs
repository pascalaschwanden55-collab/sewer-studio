using System;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Views.Windows;

// Slice 8a Pause-Confirm Step 3 + 4 — Click-Handler + Gate-Policy + Event-Mapping.
// Mini-ADR: docs/adrs/2026-05-10-slice-8a-pause-confirm.md
//
// Step 3 (Click-Handler): User-Klick im Confirmation-Panel ruft
// CompleteConfirmation auf der VM → der awaitende Loop bekommt die Decision.
// Edit-Sonderfall: pending Event vor Complete in _vm.SelectedDefect schieben,
// damit das DefectDetailPanel sofort die Edit-Bindings hat.
//
// Step 4 (Gate-Policy + EventMapping): EvaluateGate ist die lokale
// Severity-Policy (Sev * 0.20 → Konfidenz, Threshold 0.85 / 0.60), siehe
// Mini-ADR Abschnitt D. Wird durch echten QualityGate-Service ersetzt
// sobald AnalyzeWithEscalationAsync ein QualityGateResult liefert (ohne
// VM-API-Aenderung). BuildCodingEventFromFinding mappt einen Finding +
// Frame-Kontext auf einen CodingEvent fuer AddEventInOrder.
public partial class CodingModeWindow
{
    // ─── Click-Handler (Step 3) ──────────────────────────────────────

    private void ConfirmAccept_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _vm.CompleteConfirmation(CodingUserDecision.Accepted);
    }

    private void ConfirmEdit_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        // Edit-Affordance wird vom Loop nach AddEventInOrder verdrahtet:
        // _vm.SelectedDefect / LstEvents.SelectedItem / UpdateDefectDetailPanel.
        // Hier nur Decision an die VM melden — sonst wuerde SelectedDefect
        // doppelt gesetzt (gleiche Referenz, zweites PropertyChanged
        // bleibt aus, optionale Binding-Watcher koennten getaeuscht werden).
        _vm.CompleteConfirmation(CodingUserDecision.AcceptedWithEdit);
    }

    private void ConfirmReject_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _vm.CompleteConfirmation(CodingUserDecision.Rejected);
    }

    // ─── Gate-Policy + Event-Mapping (Step 4) ────────────────────────

    /// <summary>Lokale Severity-Policy (Mini-ADR Abschnitt D).
    /// Konfidenz = Severity * 0.20 (Sev 1 → 20%, Sev 5 → 100%).
    /// Threshold: >= 0.85 Green / >= 0.60 Yellow / sonst Red.</summary>
    internal static (bool isGreen, bool isYellow, bool isRed, double confidence)
        EvaluateGate(LiveFrameFinding f)
    {
        var conf = Math.Clamp(f.Severity * 0.20, 0.0, 1.0);
        if (conf >= 0.85) return (true,  false, false, conf);
        if (conf >= 0.60) return (false, true,  false, conf);
        return                    (false, false, true,  conf);
    }

    /// <summary>Mappt einen LiveFrameFinding plus Frame-Kontext auf einen
    /// CodingEvent fuer die Eventliste. Code-Heuristik: VsaCodeHint, sonst
    /// "AI" als Platzhalter — der User kann das im Edit-Pfad ueberschreiben.</summary>
    internal static CodingEvent BuildCodingEventFromFinding(
        LiveFrameFinding finding,
        double meterAtCapture,
        TimeSpan videoTimestamp,
        double confidence)
    {
        var entry = new ProtocolEntry
        {
            Code = string.IsNullOrWhiteSpace(finding.VsaCodeHint) ? "AI" : finding.VsaCodeHint!,
            Beschreibung = finding.Label ?? "",
            MeterStart = meterAtCapture,
            Zeit = videoTimestamp,
            Source = ProtocolEntrySource.Ai
        };

        if (!string.IsNullOrWhiteSpace(finding.PositionClock))
        {
            entry.CodeMeta ??= new ProtocolEntryCodeMeta { Code = entry.Code };
            entry.CodeMeta.Parameters["vsa.uhr.von"] = finding.PositionClock!;
        }

        var ev = new CodingEvent
        {
            Entry = entry,
            MeterAtCapture = meterAtCapture,
            VideoTimestamp = videoTimestamp,
            AiContext = new CodingEventAiContext
            {
                SuggestedCode = entry.Code,
                Confidence = confidence,
                Reason = finding.Label ?? ""
            }
        };

        return ev;
    }
}
