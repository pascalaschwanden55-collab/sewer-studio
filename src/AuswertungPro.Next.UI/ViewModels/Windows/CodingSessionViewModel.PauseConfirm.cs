using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

// Slice 8a Pause-Confirm Step 1a/1b — ConfirmationFlow + Sperrliste.
// Mini-ADR: docs/adrs/2026-05-10-slice-8a-pause-confirm.md
//
// Step 1a (ConfirmationFlow):
//   Loop ruft BeginConfirmationAsync(...) wenn ein Yellow/Red-Finding
//   auftaucht. VM speichert pending-state + TaskCompletionSource. UI
//   (CodingModeWindow.PauseConfirm.cs in Step 3) ruft CompleteConfirmation,
//   damit der awaitende Loop seine Decision bekommt.
//
// Step 1b (Sperrliste):
//   Wenn der User ein Finding mit "Rejected" abschmettert, soll dasselbe
//   Finding (gleicher Code + Meter ± 0.5m) im aktuellen Loop-Run nicht
//   noch einmal als Confirmation-Trigger auftauchen. Sperrliste ist
//   in-memory only (Mini-ADR-Entscheidung 2026-05-10) und wird beim
//   naechsten Session-Start wieder leer.
//
// Punkt-4-Pattern: VM ist Owner des State, Window ist nur UI-Renderer.
public sealed partial class CodingSessionViewModel
{
    // ─── Step 1a: ConfirmationFlow / TCS-State ──────────────────────────

    private TaskCompletionSource<CodingUserDecision>? _pendingTcs;
    private CancellationTokenRegistration? _pendingCtRegistration;

    /// <summary>true waehrend ein Yellow/Red-Finding auf User-Bestaetigung wartet.
    /// XAML-Binding fuer Visibility des Confirmation-Panels.</summary>
    public bool IsAwaitingUserDecision => _pendingTcs is not null;

    /// <summary>Das Event, das aktuell zur Bestaetigung ansteht. null wenn nichts pending.</summary>
    public CodingEvent? PendingConfirmationEvent { get; private set; }

    /// <summary>Konfidenz des pending Findings (0..1). null wenn nichts pending.</summary>
    public double? PendingConfirmationConfidence { get; private set; }

    /// <summary>true = Red-Zone (Konfidenz < 0.60), false = Yellow-Zone (0.60..0.85).
    /// Steuert die Ampel-Faerbung im Confirmation-Panel.</summary>
    public bool PendingConfirmationIsRed { get; private set; }

    /// <summary>Pausiert den Loop bis der User Accept/Edit/Reject klickt.
    /// Returnt die getroffene Decision; cancelt mit OperationCanceledException
    /// wenn der CancellationToken feuert.</summary>
    /// <exception cref="ArgumentNullException">ev ist null.</exception>
    /// <exception cref="InvalidOperationException">Bereits eine andere Confirmation pending.</exception>
    public Task<CodingUserDecision> BeginConfirmationAsync(
        CodingEvent ev,
        double confidence,
        bool isRed,
        CancellationToken ct)
    {
        if (ev is null) throw new ArgumentNullException(nameof(ev));
        if (_pendingTcs is not null)
            throw new InvalidOperationException(
                "BeginConfirmationAsync waehrend bereits eine Confirmation pending ist.");

        var tcs = new TaskCompletionSource<CodingUserDecision>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingTcs = tcs;
        PendingConfirmationEvent = ev;
        PendingConfirmationConfidence = confidence;
        PendingConfirmationIsRed = isRed;

        _pendingCtRegistration = ct.Register(static state =>
        {
            var self = (CodingSessionViewModel)state!;
            var tcs = self._pendingTcs;
            if (tcs is null) return;
            self.ClearPendingState();
            tcs.TrySetCanceled();
        }, this);

        OnPropertyChanged(nameof(IsAwaitingUserDecision));
        OnPropertyChanged(nameof(PendingConfirmationEvent));
        OnPropertyChanged(nameof(PendingConfirmationConfidence));
        OnPropertyChanged(nameof(PendingConfirmationIsRed));

        return tcs.Task;
    }

    /// <summary>Vom UI gerufen (Accept/Edit/Reject-Buttons): schliesst die
    /// pending Confirmation und setzt das TCS-Result. No-Op wenn nichts pending
    /// (z.B. doppel-klick).</summary>
    public void CompleteConfirmation(CodingUserDecision decision)
    {
        var tcs = _pendingTcs;
        if (tcs is null) return;
        ClearPendingState();
        tcs.TrySetResult(decision);
    }

    private void ClearPendingState()
    {
        _pendingTcs = null;
        PendingConfirmationEvent = null;
        PendingConfirmationConfidence = null;
        PendingConfirmationIsRed = false;
        _pendingCtRegistration?.Dispose();
        _pendingCtRegistration = null;

        OnPropertyChanged(nameof(IsAwaitingUserDecision));
        OnPropertyChanged(nameof(PendingConfirmationEvent));
        OnPropertyChanged(nameof(PendingConfirmationConfidence));
        OnPropertyChanged(nameof(PendingConfirmationIsRed));
    }

    // ─── Step 1b: Sperrliste / Reject-Key ───────────────────────────────

    // Tolerance-Tabelle: AI-Bucket (code-loses Finding mit Fallback "AI") wird
    // sehr eng gehalten, damit "Riss bei 12.5m" nicht semantisch wahllose
    // Findings im naechsten halben Meter mit-erschlaegt. Echte VSA-Codes
    // bleiben bei +/-0.5m.
    private const double RejectionMeterToleranceDefault = 0.5;
    private const double RejectionMeterToleranceAi = 0.1;
    private const string AiFallbackCode = "AI";

    private static double ToleranceFor(string codeNormalized)
        => codeNormalized == AiFallbackCode
            ? RejectionMeterToleranceAi
            : RejectionMeterToleranceDefault;

    // Storage: Code-Key + Label-Key (disambiguiert "AI"+"Riss" vs "AI"+"Wurzel")
    // + Meter. Alle Strings normalisiert.
    private readonly List<(string codeNormalized, string labelNormalized, double meter)> _rejections = new();

    /// <summary>Liste aller in dieser Session abgelehnten Findings als
    /// stabile Schluessel (Code|Label@Meter). Read-only, fuer Diagnostics/Logging.</summary>
    public IReadOnlyCollection<string> RejectedFindings
        => _rejections
            .Select(r => MakeRejectionKey(r.codeNormalized, r.labelNormalized, r.meter))
            .ToList();

    /// <summary>Fuegt ein Reject in die Sperrliste ein. Idempotent: gleicher
    /// Code + gleicher Label + Meter mit &lt;1cm Differenz wird nicht doppelt
    /// gespeichert.</summary>
    public void AddRejection(string code, string? label, double meter)
    {
        if (string.IsNullOrWhiteSpace(code)) return;
        var normalizedCode = code.Trim().ToUpperInvariant();
        var normalizedLabel = NormalizeLabel(label);

        for (int i = 0; i < _rejections.Count; i++)
        {
            var r = _rejections[i];
            if (r.codeNormalized == normalizedCode &&
                r.labelNormalized == normalizedLabel &&
                Math.Abs(r.meter - meter) < 0.01)
                return;
        }

        _rejections.Add((normalizedCode, normalizedLabel, meter));
    }

    /// <summary>true wenn fuer (code, label, meter) bereits ein Reject vorliegt.
    /// Toleranz +/-0.5m fuer echte Codes, +/-0.1m fuer den AI-Fallback-Bucket.
    /// Code + Label case-insensitive.</summary>
    public bool IsRejected(string code, string? label, double meter)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        var normalizedCode = code.Trim().ToUpperInvariant();
        var normalizedLabel = NormalizeLabel(label);
        var tolerance = ToleranceFor(normalizedCode);

        for (int i = 0; i < _rejections.Count; i++)
        {
            var r = _rejections[i];
            if (r.codeNormalized == normalizedCode &&
                r.labelNormalized == normalizedLabel &&
                Math.Abs(r.meter - meter) <= tolerance)
                return true;
        }
        return false;
    }

    /// <summary>Stable Key fuer ein Rejection-Entry. Format:
    /// "CODE|LABEL@MM.MM" (label leer → "CODE|@MM.MM"). Public fuer
    /// Logging/Tests.</summary>
    public static string MakeRejectionKey(string code, string? label, double meter)
    {
        var normalizedCode = (code ?? "").Trim().ToUpperInvariant();
        var normalizedLabel = NormalizeLabel(label);
        var meterStr = meter.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        return $"{normalizedCode}|{normalizedLabel}@{meterStr}";
    }

    private static string NormalizeLabel(string? label)
        => (label ?? "").Trim().ToUpperInvariant();
}
