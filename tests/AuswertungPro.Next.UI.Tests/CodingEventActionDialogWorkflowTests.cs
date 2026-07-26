using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventActionDialogWorkflowTests
{
    [Fact]
    public void ShowStretchCloseRequiresLaterMeter_offers_default_dialog_service_wiring()
    {
        var overload = typeof(CodingEventActionDialogWorkflow)
            .GetMethods()
            .SingleOrDefault(method =>
                method.Name == nameof(CodingEventActionDialogWorkflow.ShowStretchCloseRequiresLaterMeter) &&
                method.GetParameters().Length == 0);

        Assert.NotNull(overload);
    }

    [Fact]
    public void ConfirmDelete_offers_default_dialog_service_wiring()
    {
        var overload = typeof(CodingEventActionDialogWorkflow)
            .GetMethods()
            .SingleOrDefault(method =>
                method.Name == nameof(CodingEventActionDialogWorkflow.ConfirmDelete) &&
                method.GetParameters()
                    .Select(parameter => parameter.ParameterType)
                    .SequenceEqual(
                    [
                        typeof(string),
                        typeof(Func<Func<bool>, bool>),
                    ]));

        Assert.NotNull(overload);
    }

    [Fact]
    public void ShowStretchCloseRequiresLaterMeter_creates_service_and_shows_prompt()
    {
        var calls = new List<string>();
        var service = new CodingEventActionDialogService(
            (message, title) => calls.Add($"info:{message}:{title}"),
            (_, _) => throw new InvalidOperationException("Confirm must not run."));

        CodingEventActionDialogWorkflow.ShowStretchCloseRequiresLaterMeter(
            new CodingEventActionDialogWorkflowActions(
                CreateDialogService: () =>
                {
                    calls.Add("service");
                    return service;
                },
                RunWithSuspendedOverlay: _ => throw new InvalidOperationException("Suspend must not run.")));

        Assert.Equal(
            [
                "service",
                "info:Der aktuelle Meterstand muss gr\u00f6\u00dfer sein als der Anfang des Streckenschadens.:Streckenschaden"
            ],
            calls);
    }

    [Fact]
    public void ConfirmDelete_runs_dialog_inside_suspended_overlay_scope()
    {
        var calls = new List<string>();
        var service = new CodingEventActionDialogService(
            (_, _) => throw new InvalidOperationException("Info must not run."),
            (message, title) =>
            {
                calls.Add($"confirm:{message}:{title}");
                return false;
            });

        var result = CodingEventActionDialogWorkflow.ConfirmDelete(
            "BAJ",
            new CodingEventActionDialogWorkflowActions(
                CreateDialogService: () =>
                {
                    calls.Add("service");
                    return service;
                },
                RunWithSuspendedOverlay: callback =>
                {
                    calls.Add("suspend-start");
                    var confirmed = callback();
                    calls.Add("suspend-end");
                    return confirmed;
                }));

        Assert.False(result);
        Assert.Equal(
            ["suspend-start", "service", "confirm:Ereignis 'BAJ' l\u00f6schen?:L\u00f6schen", "suspend-end"],
            calls);
    }
}
