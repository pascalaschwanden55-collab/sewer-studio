using System;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolDialogServiceTests
{
    [Fact]
    public void ConfirmPdfExport_uses_event_count_and_title()
    {
        string? capturedMessage = null;
        string? capturedTitle = null;
        var service = new CodingProtocolDialogService(
            (message, title) =>
            {
                capturedMessage = message;
                capturedTitle = title;
                return true;
            },
            (_, _) => throw new InvalidOperationException("Error must not be called."));

        var confirmed = service.ConfirmPdfExport(3);

        Assert.True(confirmed);
        Assert.Contains("3 Ereignisse", capturedMessage);
        Assert.Contains("PDF-Protokoll", capturedMessage);
        Assert.Equal("PDF-Protokoll erstellen", capturedTitle);
    }

    [Fact]
    public void ConfirmProtocolPreview_uses_observation_count_and_title()
    {
        string? capturedMessage = null;
        string? capturedTitle = null;
        var service = new CodingProtocolDialogService(
            (message, title) =>
            {
                capturedMessage = message;
                capturedTitle = title;
                return false;
            },
            (_, _) => throw new InvalidOperationException("Error must not be called."));

        var confirmed = service.ConfirmProtocolPreview(5);

        Assert.False(confirmed);
        Assert.Contains("5 Beobachtungen", capturedMessage);
        Assert.Contains("Protokoll jetzt anzeigen", capturedMessage);
        Assert.Equal("Codier-Session abgeschlossen", capturedTitle);
    }

    [Fact]
    public void ShowPdfExportFailed_uses_error_dialog()
    {
        string? capturedMessage = null;
        string? capturedTitle = null;
        var service = new CodingProtocolDialogService(
            (_, _) => throw new InvalidOperationException("Confirm must not be called."),
            (message, title) =>
            {
                capturedMessage = message;
                capturedTitle = title;
            });

        service.ShowPdfExportFailed("Datentraeger voll");

        Assert.Equal("PDF konnte nicht erstellt werden:\nDatentraeger voll", capturedMessage);
        Assert.Equal("Fehler", capturedTitle);
    }

    [Fact]
    public void Factory_creates_dialog_service()
    {
        var service = CodingProtocolDialogServiceFactory.Create();

        Assert.NotNull(service);
    }
}
