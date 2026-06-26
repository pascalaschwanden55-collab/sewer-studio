using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingUnappliedChangesCloseDialogWorkflowTests
{
    [Fact]
    public void Execute_offers_default_dialog_service_wiring()
    {
        var overload = typeof(CodingUnappliedChangesCloseDialogWorkflow)
            .GetMethods()
            .SingleOrDefault(method =>
                method.Name == nameof(CodingUnappliedChangesCloseDialogWorkflow.Execute) &&
                method.GetParameters()
                    .Select(parameter => parameter.ParameterType)
                    .SequenceEqual(
                    [
                        typeof(Func<Func<bool>, bool>),
                        typeof(Func<bool>),
                    ]));

        Assert.NotNull(overload);
    }

    [Fact]
    public void Execute_runs_unapplied_close_dialog_inside_suspended_overlay_scope()
    {
        var calls = new List<string>();
        var service = new CodingApplyDialogService(
            (_, _) => throw new InvalidOperationException("Empty protocol confirm should not run."),
            (_, _) =>
            {
                calls.Add("dialog");
                return DialogConfirm.Yes;
            });

        var result = CodingUnappliedChangesCloseDialogWorkflow.Execute(
            new CodingUnappliedChangesCloseDialogWorkflowActions(
                RunWithSuspendedOverlay: callback =>
                {
                    calls.Add("suspend-start");
                    var shouldClose = callback();
                    calls.Add("suspend-end");
                    return shouldClose;
                },
                CreateDialogService: () =>
                {
                    calls.Add("service");
                    return service;
                },
                ApplyChanges: () =>
                {
                    calls.Add("apply");
                    return true;
                }));

        Assert.True(result);
        Assert.Equal(["suspend-start", "service", "dialog", "apply", "suspend-end"], calls);
    }
}
