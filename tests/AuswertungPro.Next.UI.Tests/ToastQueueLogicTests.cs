using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Tests fuer die UI-freie Toast-Warteschlange: max. 3 sichtbar, FIFO-Nachruecken,
/// Ablauf nach Schwere (Success/Info 3 s, Warning 5 s, Error bleibt), Dismiss, leere Meldung.
/// Zeit wird als monotone ms hereingereicht -> kein Timer, keine Uhr im Test.
/// </summary>
public sealed class ToastQueueLogicTests
{
    [Fact]
    public void Show_makes_toast_visible()
    {
        var logic = new ToastQueueLogic();

        var id = logic.Show("Export fertig", ToastSeverity.Success, nowMs: 0);

        Assert.NotNull(id);
        Assert.Single(logic.Visible);
        Assert.Equal("Export fertig", logic.Visible[0].Message);
        Assert.Equal(ToastSeverity.Success, logic.Visible[0].Severity);
    }

    // ── Restzeit fuer die ablaufende Lebenslinie im Toast ──

    [Fact]
    public void RemainingMs_counts_down_over_the_display_time()
    {
        var logic = new ToastQueueLogic();
        var id = logic.Show("Export fertig", ToastSeverity.Success, nowMs: 1000)!.Value;

        Assert.Equal(3000, logic.RemainingMs(id, nowMs: 1000));
        Assert.Equal(1800, logic.RemainingMs(id, nowMs: 2200));
    }

    [Fact]
    public void RemainingMs_never_goes_negative()
    {
        var logic = new ToastQueueLogic();
        var id = logic.Show("Export fertig", ToastSeverity.Info, nowMs: 0)!.Value;

        Assert.Equal(0, logic.RemainingMs(id, nowMs: 99_000));
    }

    [Fact]
    public void RemainingMs_is_null_for_errors_that_stay_until_clicked()
    {
        var logic = new ToastQueueLogic();
        var id = logic.Show("Import fehlgeschlagen", ToastSeverity.Error, nowMs: 0)!.Value;

        Assert.Null(logic.RemainingMs(id, nowMs: 0));
    }

    [Fact]
    public void RemainingMs_is_null_for_unknown_and_waiting_toasts()
    {
        var logic = new ToastQueueLogic();
        for (var i = 0; i < ToastQueueLogic.MaxVisible; i++)
            logic.Show($"Sichtbar {i}", ToastSeverity.Info, nowMs: 0);

        // Der vierte wartet noch — seine Anzeigezeit laeuft erst beim Nachruecken.
        var waiting = logic.Show("Wartet", ToastSeverity.Info, nowMs: 0)!.Value;

        Assert.Null(logic.RemainingMs(waiting, nowMs: 0));
        Assert.Null(logic.RemainingMs(id: 9999, nowMs: 0));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Show_rejects_empty_message(string message)
    {
        var logic = new ToastQueueLogic();

        var id = logic.Show(message, ToastSeverity.Info, nowMs: 0);

        Assert.Null(id);
        Assert.Empty(logic.Visible);
    }

    [Fact]
    public void Show_trims_message()
    {
        var logic = new ToastQueueLogic();

        logic.Show("  hallo  ", ToastSeverity.Info, nowMs: 0);

        Assert.Equal("hallo", logic.Visible[0].Message);
    }

    [Fact]
    public void Show_caps_visible_at_three_and_queues_rest()
    {
        var logic = new ToastQueueLogic();

        for (var i = 0; i < 5; i++)
            logic.Show($"m{i}", ToastSeverity.Success, nowMs: 0);

        Assert.Equal(3, logic.Visible.Count);
        Assert.Equal(2, logic.PendingCount);
    }

    [Fact]
    public void Prune_expires_success_after_three_seconds()
    {
        var logic = new ToastQueueLogic();
        logic.Show("m", ToastSeverity.Success, nowMs: 0);

        logic.Prune(nowMs: 2999);
        Assert.Single(logic.Visible);

        logic.Prune(nowMs: 3000);
        Assert.Empty(logic.Visible);
    }

    [Fact]
    public void Prune_keeps_warning_until_five_seconds()
    {
        var logic = new ToastQueueLogic();
        logic.Show("m", ToastSeverity.Warning, nowMs: 0);

        logic.Prune(nowMs: 3000);
        Assert.Single(logic.Visible);

        logic.Prune(nowMs: 5000);
        Assert.Empty(logic.Visible);
    }

    [Fact]
    public void Prune_never_expires_error()
    {
        var logic = new ToastQueueLogic();
        logic.Show("boom", ToastSeverity.Error, nowMs: 0);

        logic.Prune(nowMs: 1_000_000);

        Assert.Single(logic.Visible);
    }

    [Fact]
    public void Prune_promotes_pending_when_slot_frees()
    {
        var logic = new ToastQueueLogic();
        logic.Show("a", ToastSeverity.Success, nowMs: 0);
        logic.Show("b", ToastSeverity.Success, nowMs: 0);
        logic.Show("c", ToastSeverity.Success, nowMs: 0);
        var pendingId = logic.Show("d", ToastSeverity.Warning, nowMs: 0);

        logic.Prune(nowMs: 3000); // a,b,c laufen ab -> d rueckt nach

        Assert.Single(logic.Visible);
        Assert.Equal(pendingId, logic.Visible[0].Id);
        Assert.Equal(0, logic.PendingCount);
    }

    [Fact]
    public void Promoted_toast_countdown_starts_at_promotion_time()
    {
        var logic = new ToastQueueLogic();
        logic.Show("a", ToastSeverity.Success, nowMs: 0);
        logic.Show("b", ToastSeverity.Success, nowMs: 0);
        logic.Show("c", ToastSeverity.Success, nowMs: 0);
        logic.Show("d", ToastSeverity.Success, nowMs: 0); // wartet

        logic.Prune(nowMs: 3000);  // a,b,c weg, d wird bei 3000 sichtbar
        Assert.Single(logic.Visible);

        logic.Prune(nowMs: 5000);  // d-Alter 2000 < 3000 -> bleibt
        Assert.Single(logic.Visible);

        logic.Prune(nowMs: 6000);  // d-Alter 3000 -> laeuft ab
        Assert.Empty(logic.Visible);
    }

    [Fact]
    public void Dismiss_removes_visible_toast()
    {
        var logic = new ToastQueueLogic();
        var id = logic.Show("m", ToastSeverity.Error, nowMs: 0);

        logic.Dismiss(id!.Value, nowMs: 100);

        Assert.Empty(logic.Visible);
    }

    [Fact]
    public void Dismiss_promotes_pending()
    {
        var logic = new ToastQueueLogic();
        var a = logic.Show("a", ToastSeverity.Error, nowMs: 0);
        var b = logic.Show("b", ToastSeverity.Error, nowMs: 0);
        var c = logic.Show("c", ToastSeverity.Error, nowMs: 0);
        var d = logic.Show("d", ToastSeverity.Error, nowMs: 0); // wartet

        logic.Dismiss(a!.Value, nowMs: 100);

        Assert.Equal(3, logic.Visible.Count);
        Assert.Equal(
            new[] { b!.Value, c!.Value, d!.Value },
            logic.Visible.Select(t => t.Id));
    }
}
