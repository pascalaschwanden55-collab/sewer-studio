using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingModeDialogWorkflowTests
{
    [Fact]
    public void ShowMissingHaltung_offers_default_dialog_service_wiring()
    {
        var overload = typeof(CodingModeDialogWorkflow)
            .GetMethods()
            .SingleOrDefault(method =>
                method.Name == nameof(CodingModeDialogWorkflow.ShowMissingHaltung) &&
                method.GetParameters().Length == 0);

        Assert.NotNull(overload);
    }

    [Fact]
    public void ShowSessionStartFailed_offers_default_dialog_service_wiring()
    {
        var overload = typeof(CodingModeDialogWorkflow)
            .GetMethods()
            .SingleOrDefault(method =>
                method.Name == nameof(CodingModeDialogWorkflow.ShowSessionStartFailed) &&
                method.GetParameters()
                    .Select(parameter => parameter.ParameterType)
                    .SequenceEqual([typeof(string)]));

        Assert.NotNull(overload);
    }

    [Fact]
    public void ShowMissingHaltung_creates_service_and_shows_missing_haltung_dialog()
    {
        var calls = new List<string>();
        var service = Service(calls);

        CodingModeDialogWorkflow.ShowMissingHaltung(
            new CodingModeDialogWorkflowActions(
                CreateDialogService: () =>
                {
                    calls.Add("service");
                    return service;
                }));

        Assert.Equal(["service", "missing"], calls);
    }

    [Fact]
    public void ShowSessionStartFailed_creates_service_and_shows_warning()
    {
        var calls = new List<string>();
        var service = Service(calls);

        CodingModeDialogWorkflow.ShowSessionStartFailed(
            "Laenge fehlt",
            new CodingModeDialogWorkflowActions(
                CreateDialogService: () =>
                {
                    calls.Add("service");
                    return service;
                }));

        Assert.Equal(["service", "failed:Laenge fehlt"], calls);
    }

    private static CodingModeDialogService Service(List<string> calls)
        => new(
            (_, _) => calls.Add("missing"),
            (message, _) => calls.Add($"failed:{message}"));
}
