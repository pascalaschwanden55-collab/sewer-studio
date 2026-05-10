using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

// Slice 8a Pause-Confirm Step 1a — ConfirmationFlow / TCS-State.
// Mini-ADR: docs/adrs/2026-05-10-slice-8a-pause-confirm.md
//
// Loop ruft BeginConfirmationAsync(...) wenn ein Yellow/Red-Finding
// auftaucht. VM speichert pending-state + TaskCompletionSource. UI
// (CodingModeWindow.PauseConfirm.cs in Step 3) ruft CompleteConfirmation,
// damit der awaitende Loop seine Decision bekommt.
//
// Punkt-4-Pattern: VM ist Owner des State, Window ist nur UI-Renderer.
public sealed partial class CodingSessionViewModel
{
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
}
