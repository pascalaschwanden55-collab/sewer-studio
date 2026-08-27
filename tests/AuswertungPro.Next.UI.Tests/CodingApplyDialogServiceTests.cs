using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingApplyDialogServiceTests
{
    [Fact]
    public void ConfirmEmptyProtocol_skips_dialog_when_guard_does_not_require_confirmation()
    {
        var service = new CodingApplyDialogService(
            (_, _) => throw new InvalidOperationException("ConfirmWarn must not be called."),
            (_, _) => throw new InvalidOperationException("ConfirmCancel must not be called."));

        var result = service.ConfirmEmptyProtocol(
            new CodingApplyEmptyProtocolGuardResult(
                RequiresConfirmation: false,
                Message: string.Empty,
                Title: string.Empty));

        Assert.True(result);
    }

    [Fact]
    public void ConfirmEmptyProtocol_delegates_required_confirmation()
    {
        string? capturedMessage = null;
        string? capturedTitle = null;
        var service = new CodingApplyDialogService(
            (message, title) =>
            {
                capturedMessage = message;
                capturedTitle = title;
                return false;
            },
            (_, _) => throw new InvalidOperationException("ConfirmCancel must not be called."));

        var result = service.ConfirmEmptyProtocol(
            new CodingApplyEmptyProtocolGuardResult(
                RequiresConfirmation: true,
                Message: "Befunde wirklich loeschen?",
                Title: "Leere Codierung"));

        Assert.False(result);
        Assert.Equal("Befunde wirklich loeschen?", capturedMessage);
        Assert.Equal("Leere Codierung", capturedTitle);
    }

    [Fact]
    public void ConfirmUnappliedChangesOnClose_uses_close_prompt_and_policy()
    {
        string? capturedMessage = null;
        string? capturedTitle = null;
        var applyCalled = false;
        var service = new CodingApplyDialogService(
            (_, _) => throw new InvalidOperationException("ConfirmWarn must not be called."),
            (message, title) =>
            {
                capturedMessage = message;
                capturedTitle = title;
                return DialogConfirm.No;
            });

        var result = service.ConfirmUnappliedChangesOnClose(() =>
        {
            applyCalled = true;
            return false;
        });

        Assert.True(result);
        Assert.False(applyCalled);
        Assert.Contains("nicht \u00fcbernommene Codierungen", capturedMessage);
        Assert.Contains("Ja = \u00fcbernehmen", capturedMessage);
        Assert.Contains("Abbrechen = Fenster offen lassen", capturedMessage);
        Assert.Equal("Codier-Modus", capturedTitle);
    }

    [Fact]
    public void ConfirmUnappliedChangesOnClose_returns_apply_result_for_yes()
    {
        var service = new CodingApplyDialogService(
            (_, _) => throw new InvalidOperationException("ConfirmWarn must not be called."),
            (_, _) => DialogConfirm.Yes);

        var result = service.ConfirmUnappliedChangesOnClose(() => false);

        Assert.False(result);
    }

    /// <summary>
    /// Eine bekannte, aber unbrauchbare Haltungslaenge darf nicht als "nicht
    /// bekannt" ausgegeben werden. Der Codiermodus startet ohne Laenge gar nicht -
    /// dieser Satz waere im Programm also immer falsch. Der Benutzer muss lesen,
    /// welche Zahl verworfen wurde und warum, sonst kann er nichts damit anfangen.
    /// </summary>
    [Fact]
    public void ConfirmMissingPipeEnd_nennt_die_verworfene_Laenge_statt_sie_zu_leugnen()
    {
        string? nachricht = null;
        var service = new CodingApplyDialogService(
            (message, _) =>
            {
                nachricht = message;
                return true;
            },
            (_, _) => throw new InvalidOperationException("ConfirmCancel darf nicht laufen."));

        var entscheidung = service.ConfirmMissingPipeEnd(
            new CodingApplyPipeEndPrompt(
                ProposalMeter: null,
                RejectedLengthM: 12.00,
                LastObservationM: 18.50));

        Assert.Equal(CodingApplyPipeEndDecision.Skip, entscheidung);
        Assert.NotNull(nachricht);
        var text = nachricht!.Replace(',', '.');

        Assert.DoesNotContain("nicht bekannt", text, StringComparison.Ordinal);
        // BEIDE Zahlen, sonst wuerde ein Text durchgehen, der zweimal dieselbe nennt.
        Assert.Contains("12.00", text, StringComparison.Ordinal);
        Assert.Contains("18.50", text, StringComparison.Ordinal);

        // "gespeichert" waere gelogen: im haeufigsten Fall hat der Codiermodus die
        // Laenge beim Einstieg selbst aus dem hoechsten Protokollmeter abgeleitet.
        Assert.DoesNotContain("gespeichert", text, StringComparison.OrdinalIgnoreCase);

        // Und kein Rat, der ausserhalb des Protokolls wirkt: Haltungslaenge_m
        // steuert auch laengenbasierte Kostenpositionen und den PDF-Export.
        Assert.DoesNotContain("korrigiere die Haltungsl", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Der haeufigste Fall: Die Laenge sitzt GENAU auf dem letzten Befund, weil der
    /// Codiermodus sie von dort abgeleitet hat. Dann darf der Satz nicht zweimal
    /// dieselbe Zahl nennen - das liest sich wie ein Fehler im Programm.
    /// </summary>
    [Fact]
    public void ConfirmMissingPipeEnd_nennt_bei_gleichem_Meter_nur_eine_Zahl()
    {
        string? nachricht = null;
        var service = new CodingApplyDialogService(
            (message, _) =>
            {
                nachricht = message;
                return true;
            },
            (_, _) => throw new InvalidOperationException("ConfirmCancel darf nicht laufen."));

        service.ConfirmMissingPipeEnd(
            new CodingApplyPipeEndPrompt(
                ProposalMeter: null,
                RejectedLengthM: 20.31,
                LastObservationM: 20.31));

        var text = nachricht!.Replace(',', '.');
        var treffer = text.Split("20.31").Length - 1;

        Assert.Equal(1, treffer);
        Assert.DoesNotContain("nicht bekannt", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Der Fall ohne jede Laenge bleibt erhalten - er ist im Codiermodus zwar
    /// nicht erreichbar, der Dienst ist aber auch von aussen aufrufbar.
    /// </summary>
    [Fact]
    public void ConfirmMissingPipeEnd_meldet_eine_wirklich_fehlende_Laenge_weiterhin_als_unbekannt()
    {
        string? nachricht = null;
        var service = new CodingApplyDialogService(
            (message, _) =>
            {
                nachricht = message;
                return false;
            },
            (_, _) => throw new InvalidOperationException("ConfirmCancel darf nicht laufen."));

        var entscheidung = service.ConfirmMissingPipeEnd(new CodingApplyPipeEndPrompt(null));

        Assert.Equal(CodingApplyPipeEndDecision.Cancel, entscheidung);
        Assert.Contains("nicht bekannt", nachricht!, StringComparison.Ordinal);
    }

    [Fact]
    public void Factory_creates_dialog_service()
    {
        var service = CodingApplyDialogServiceFactory.Create();

        Assert.NotNull(service);
    }
}
