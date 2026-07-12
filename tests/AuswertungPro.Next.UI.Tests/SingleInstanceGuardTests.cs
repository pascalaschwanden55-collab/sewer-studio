using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SingleInstanceGuardTests
{
    [Fact]
    public void TryAcquire_ZweiteInstanzErhaeltFalse()
    {
        var mutexName = @"Local\SewerStudio.Test." + Guid.NewGuid().ToString("N");
        using var acquired = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        Exception? workerError = null;

        var firstThread = new Thread(() =>
        {
            try
            {
                using var first = new SingleInstanceGuard(mutexName);
                Assert.True(first.TryAcquire());
                acquired.Set();
                release.Wait(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                workerError = ex;
                acquired.Set();
            }
        });

        firstThread.Start();
        Assert.True(acquired.Wait(TimeSpan.FromSeconds(10)));

        try
        {
            Assert.Null(workerError);
            using var second = new SingleInstanceGuard(mutexName);
            Assert.False(second.TryAcquire());
        }
        finally
        {
            release.Set();
            firstThread.Join(TimeSpan.FromSeconds(10));
        }

        Assert.Null(workerError);
        Assert.False(firstThread.IsAlive);
    }
}
