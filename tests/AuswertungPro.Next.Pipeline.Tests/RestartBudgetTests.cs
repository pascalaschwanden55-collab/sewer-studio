using System;
using AuswertungPro.Next.Application.Common;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>Tests fuer RestartBudget (Sidecar-Watchdog Slide-Window-Logik).</summary>
public class RestartBudgetTests
{
    [Fact]
    public void TryConsume_FirstThree_AllSucceed()
    {
        var budget = new RestartBudget { MaxRestartsPerWindow = 3, Window = TimeSpan.FromMinutes(5) };
        var t0 = DateTime.UtcNow;

        Assert.True(budget.TryConsume(t0));
        Assert.True(budget.TryConsume(t0.AddSeconds(10)));
        Assert.True(budget.TryConsume(t0.AddSeconds(20)));
    }

    [Fact]
    public void TryConsume_FourthInWindow_Fails()
    {
        var budget = new RestartBudget { MaxRestartsPerWindow = 3, Window = TimeSpan.FromMinutes(5) };
        var t0 = DateTime.UtcNow;

        budget.TryConsume(t0);
        budget.TryConsume(t0.AddSeconds(10));
        budget.TryConsume(t0.AddSeconds(20));

        Assert.False(budget.TryConsume(t0.AddSeconds(30)));
    }

    [Fact]
    public void TryConsume_AfterWindowExpired_BudgetRefilled()
    {
        var budget = new RestartBudget { MaxRestartsPerWindow = 3, Window = TimeSpan.FromMinutes(5) };
        var t0 = DateTime.UtcNow;

        budget.TryConsume(t0);
        budget.TryConsume(t0.AddSeconds(10));
        budget.TryConsume(t0.AddSeconds(20));

        // 6 Min spaeter — alle 3 alten sind ausserhalb des Fensters
        var t1 = t0.AddMinutes(6);
        Assert.True(budget.TryConsume(t1));
        Assert.Equal(1, budget.RecentCount(t1));
    }

    [Fact]
    public void RecentCount_DropsAsTimePasses()
    {
        var budget = new RestartBudget { MaxRestartsPerWindow = 5, Window = TimeSpan.FromMinutes(5) };
        var t0 = DateTime.UtcNow;

        budget.TryConsume(t0);
        budget.TryConsume(t0.AddMinutes(1));
        budget.TryConsume(t0.AddMinutes(2));

        Assert.Equal(3, budget.RecentCount(t0.AddMinutes(2.5)));

        // Der erste rutscht raus 5 Min nach t0 → bei t0+5.5 nur noch 2
        Assert.Equal(2, budget.RecentCount(t0.AddMinutes(5.5)));
    }

    [Fact]
    public void ComputeBackoff_GrowsWithAttempts_CappedAt60s()
    {
        var budget = new RestartBudget { Window = TimeSpan.FromMinutes(5) };
        var t = DateTime.UtcNow;

        // 0 vergangene → 10 s
        Assert.Equal(TimeSpan.FromSeconds(10), budget.ComputeBackoff(t));

        budget.TryConsume(t);
        Assert.Equal(TimeSpan.FromSeconds(20), budget.ComputeBackoff(t));

        budget.TryConsume(t.AddSeconds(1));
        Assert.Equal(TimeSpan.FromSeconds(30), budget.ComputeBackoff(t.AddSeconds(2)));

        // Cap bei 60 s
        for (int i = 0; i < 20; i++) budget.TryConsume(t.AddSeconds(2 + i));
        Assert.True(budget.ComputeBackoff(t.AddSeconds(25)) <= TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void Reset_ClearsBudget()
    {
        var budget = new RestartBudget { MaxRestartsPerWindow = 1 };
        var t = DateTime.UtcNow;

        budget.TryConsume(t);
        Assert.False(budget.TryConsume(t.AddSeconds(1)));

        budget.Reset();
        Assert.True(budget.TryConsume(t.AddSeconds(2)));
    }
}
