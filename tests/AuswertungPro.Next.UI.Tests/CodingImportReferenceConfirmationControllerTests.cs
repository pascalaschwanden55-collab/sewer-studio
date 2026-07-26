using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingImportReferenceConfirmationControllerTests
{
    [Fact]
    public async Task ExecuteAsync_IgnoriertFehlendeAuswahl()
    {
        var calls = new List<string>();
        var controller = new CodingImportReferenceConfirmationController();

        var result = await controller.ExecuteAsync(null, Actions(calls));

        Assert.Equal(CodingImportReferenceConfirmationOutcome.MissingSelection, result);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task ExecuteAsync_VerlangtVsaCodeVorBestaetigung()
    {
        var calls = new List<string>();
        var controller = new CodingImportReferenceConfirmationController();

        var result = await controller.ExecuteAsync(Event("  "), Actions(calls));

        Assert.Equal(CodingImportReferenceConfirmationOutcome.MissingCode, result);
        Assert.Equal(["missing-code"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_Persistenzfehler_zeigt_Fehler_aber_keinen_Erfolg()
    {
        var calls = new List<string>();
        var controller = new CodingImportReferenceConfirmationController();

        var result = await controller.ExecuteAsync(
            Event("BCA"),
            Actions(
                calls,
                persistWithResult: _ => Task.FromResult(
                    CodingTrainingSamplePersistenceResult.Failed("JSON gesperrt"))));

        Assert.Equal(CodingImportReferenceConfirmationOutcome.PersistenceFailed, result);
        Assert.Equal(["error:JSON gesperrt"], calls);
        Assert.DoesNotContain("success", calls);
        Assert.DoesNotContain("refresh-match", calls);
    }

    [Fact]
    public async Task ExecuteAsync_BestaetigtSpeichertUndAktualisiertInDieserReihenfolge()
    {
        var calls = new List<string>();
        var selectedEvent = Event("BCA");
        var controller = new CodingImportReferenceConfirmationController();

        var result = await controller.ExecuteAsync(selectedEvent, Actions(calls));

        Assert.Equal(CodingImportReferenceConfirmationOutcome.Confirmed, result);
        Assert.Equal(["persist:BCA", "success", "refresh-match"], calls);
        Assert.Equal(CodingUserDecision.Accepted, selectedEvent.ReviewContext?.Decision);
        Assert.Equal("Import bestaetigt (ins Brain)", selectedEvent.ReviewContext?.Reason);
    }

    private static CodingEvent Event(string code)
        => new() { Entry = new ProtocolEntry { Code = code } };

    private static CodingImportReferenceConfirmationActions Actions(
        List<string> calls,
        Func<CodingEvent, Task<CodingTrainingSamplePersistenceResult>>? persistWithResult = null)
        => new(
            ShowMissingCode: () => calls.Add("missing-code"),
            PersistTrainingSampleAsync: codingEvent =>
            {
                calls.Add($"persist:{codingEvent.Entry.Code}");
                return Task.CompletedTask;
            },
            ShowSuccess: () => calls.Add("success"),
            RefreshProtocolMatch: () => calls.Add("refresh-match"),
            PersistTrainingSampleWithResultAsync: persistWithResult,
            ShowPersistenceError: error => calls.Add($"error:{error}"));
}
