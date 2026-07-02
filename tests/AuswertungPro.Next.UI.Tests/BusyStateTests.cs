using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Tests fuer die UI-freie Busy-Logik: zaehlerbasiert (verschachtelte Langlaeufer),
/// Meldung faellt beim Verlassen auf den umgebenden Vorgang zurueck, Dispose idempotent.
/// </summary>
public sealed class BusyStateTests
{
    [Fact]
    public void Enter_activates_and_sets_message()
    {
        var busy = new BusyState();
        Assert.False(busy.IsActive);

        busy.Enter("Export läuft");

        Assert.True(busy.IsActive);
        Assert.Equal("Export läuft", busy.Message);
    }

    [Fact]
    public void Dispose_deactivates_when_last_scope_ends()
    {
        var busy = new BusyState();
        var scope = busy.Enter("x");

        scope.Dispose();

        Assert.False(busy.IsActive);
        Assert.Equal("", busy.Message);
    }

    [Fact]
    public void Nested_scopes_stay_active_until_all_disposed()
    {
        var busy = new BusyState();
        var outer = busy.Enter("outer");
        var inner = busy.Enter("inner");
        Assert.Equal("inner", busy.Message);

        inner.Dispose();
        Assert.True(busy.IsActive);            // outer haelt aktiv
        Assert.Equal("outer", busy.Message);   // Meldung faellt zurueck

        outer.Dispose();
        Assert.False(busy.IsActive);
    }

    [Fact]
    public void Dispose_is_idempotent_and_order_independent()
    {
        var busy = new BusyState();
        var a = busy.Enter("a");
        busy.Enter("b");

        a.Dispose();
        a.Dispose(); // zweiter Dispose darf "b" nicht mit entfernen / nicht negativ werden

        Assert.True(busy.IsActive);
        Assert.Equal("b", busy.Message);
    }
}
