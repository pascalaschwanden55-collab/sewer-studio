using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingApplyEmptyProtocolDialogWorkflowTests
{
    [Fact]
    public void Execute_offers_default_dialog_service_wiring()
    {
        var overload = typeof(CodingApplyEmptyProtocolDialogWorkflow)
            .GetMethods()
            .SingleOrDefault(method =>
                method.Name == nameof(CodingApplyEmptyProtocolDialogWorkflow.Execute) &&
                method.GetParameters()
                    .Select(parameter => parameter.ParameterType)
                    .SequenceEqual([typeof(CodingApplyEmptyProtocolGuardResult)]));

        Assert.NotNull(overload);
    }

    [Fact]
    public void Execute_creates_dialog_service_and_delegates_empty_protocol_confirmation()
    {
        var calls = new List<string>();
        var service = new CodingApplyDialogService(
            (message, title) =>
            {
                calls.Add($"dialog:{message}:{title}");
                return false;
            },
            (_, _) => throw new InvalidOperationException("Close confirm should not run."));

        var result = CodingApplyEmptyProtocolDialogWorkflow.Execute(
            new CodingApplyEmptyProtocolGuardResult(
                RequiresConfirmation: true,
                Message: "Befunde wirklich loeschen?",
                Title: "Leere Codierung"),
            new CodingApplyEmptyProtocolDialogWorkflowActions(
                CreateDialogService: () =>
                {
                    calls.Add("service");
                    return service;
                }));

        Assert.False(result);
        Assert.Equal(["service", "dialog:Befunde wirklich loeschen?:Leere Codierung"], calls);
    }
}
