using System;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingOpenStretchDamageDialogDecision
{
    Close,
    Continue,
    Cancel
}

public sealed class CodingOpenStretchDamageDialogService
{
    private readonly Func<string, string, DialogConfirm> _confirmCancel;

    public CodingOpenStretchDamageDialogService(Func<string, string, DialogConfirm> confirmCancel)
    {
        _confirmCancel = confirmCancel;
    }

    public CodingOpenStretchDamageDialogDecision ConfirmClose(
        IReadOnlyList<CodingEvent> openEvents,
        double currentMeter)
    {
        var prompt = CodingOpenStretchDamagePromptBuilder.Build(openEvents, currentMeter);
        var result = _confirmCancel(prompt, "Offene Streckensch\u00e4den");

        return result switch
        {
            DialogConfirm.Yes => CodingOpenStretchDamageDialogDecision.Close,
            DialogConfirm.Cancel => CodingOpenStretchDamageDialogDecision.Cancel,
            _ => CodingOpenStretchDamageDialogDecision.Continue
        };
    }
}
