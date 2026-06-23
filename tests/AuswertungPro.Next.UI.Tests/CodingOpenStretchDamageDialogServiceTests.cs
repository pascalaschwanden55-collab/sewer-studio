using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOpenStretchDamageDialogServiceTests
{
    [Fact]
    public void ConfirmClose_builds_prompt_and_maps_yes_to_close()
    {
        string? capturedMessage = null;
        string? capturedTitle = null;
        var service = new CodingOpenStretchDamageDialogService(
            (message, title) =>
            {
                capturedMessage = message;
                capturedTitle = title;
                return DialogConfirm.Yes;
            });

        var decision = service.ConfirmClose([Event("BAB", "Riss laengs")], currentMeter: 4.25);

        Assert.Equal(CodingOpenStretchDamageDialogDecision.Close, decision);
        Assert.Contains("BAB", capturedMessage);
        Assert.Contains("Riss laengs", capturedMessage);
        Assert.Contains("4", capturedMessage);
        Assert.Equal("Offene Streckensch\u00e4den", capturedTitle);
    }

    [Fact]
    public void ConfirmClose_maps_no_to_continue()
    {
        var service = new CodingOpenStretchDamageDialogService((_, _) => DialogConfirm.No);

        var decision = service.ConfirmClose([Event("BAB", "Riss laengs")], currentMeter: 4.25);

        Assert.Equal(CodingOpenStretchDamageDialogDecision.Continue, decision);
    }

    [Fact]
    public void ConfirmClose_maps_cancel_to_cancel()
    {
        var service = new CodingOpenStretchDamageDialogService((_, _) => DialogConfirm.Cancel);

        var decision = service.ConfirmClose([Event("BAB", "Riss laengs")], currentMeter: 4.25);

        Assert.Equal(CodingOpenStretchDamageDialogDecision.Cancel, decision);
    }

    [Fact]
    public void Factory_creates_dialog_service()
    {
        var service = CodingOpenStretchDamageDialogServiceFactory.Create();

        Assert.NotNull(service);
    }

    private static CodingEvent Event(string code, string description)
        => new()
        {
            Entry = new ProtocolEntry
            {
                Code = code,
                Beschreibung = description,
                IsStreckenschaden = true,
                MeterStart = 1.5
            },
            MeterAtCapture = 1.5
        };
}
