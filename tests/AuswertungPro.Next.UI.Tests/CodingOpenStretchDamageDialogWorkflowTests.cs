using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOpenStretchDamageDialogWorkflowTests
{
    [Fact]
    public void ConfirmClose_offers_default_dialog_service_wiring()
    {
        var overload = typeof(CodingOpenStretchDamageDialogWorkflow)
            .GetMethods()
            .SingleOrDefault(method =>
                method.Name == nameof(CodingOpenStretchDamageDialogWorkflow.ConfirmClose) &&
                method.GetParameters()
                    .Select(parameter => parameter.ParameterType)
                    .SequenceEqual(
                    [
                        typeof(IReadOnlyList<CodingEvent>),
                        typeof(double),
                        typeof(Func<Func<CodingOpenStretchDamageDialogDecision>, CodingOpenStretchDamageDialogDecision>),
                    ]));

        Assert.NotNull(overload);
    }

    [Fact]
    public void ConfirmClose_runs_dialog_inside_suspended_overlay_scope()
    {
        var calls = new List<string>();
        var service = new CodingOpenStretchDamageDialogService(
            (_, _) =>
            {
                calls.Add("dialog");
                return DialogConfirm.No;
            });
        var openEvents = new[] { Event("BAJ") };

        var decision = CodingOpenStretchDamageDialogWorkflow.ConfirmClose(
            openEvents,
            closeMeter: 4.25,
            new CodingOpenStretchDamageDialogWorkflowActions(
                RunWithSuspendedOverlay: callback =>
                {
                    calls.Add("suspend-start");
                    var result = callback();
                    calls.Add("suspend-end");
                    return result;
                },
                CreateDialogService: () =>
                {
                    calls.Add("service");
                    return service;
                }));

        Assert.Equal(CodingOpenStretchDamageDialogDecision.Continue, decision);
        Assert.Equal(["suspend-start", "service", "dialog", "suspend-end"], calls);
    }

    private static CodingEvent Event(string code)
        => new()
        {
            Entry = new ProtocolEntry
            {
                Code = code,
                Beschreibung = "Riss",
                IsStreckenschaden = true,
                MeterStart = 1.5
            },
            MeterAtCapture = 1.5
        };
}
