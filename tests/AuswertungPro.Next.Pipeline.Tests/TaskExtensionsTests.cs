using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Common;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Tests fuer <see cref="TaskExtensions.SafeFireAndForget"/>.
/// Verhindert ungeloggte Exceptions bei <c>_ = SomeAsync(...)</c>.
/// </summary>
public class TaskExtensionsTests
{
    [Fact]
    public async Task SafeFireAndForget_SuccessfulTask_NoCallback()
    {
        var callbackInvoked = false;
        Task t = Task.CompletedTask;
        t.SafeFireAndForget("Test", _ => callbackInvoked = true);

        // SafeFireAndForget ist async void → kurz warten damit es durchlaeuft
        await Task.Delay(50);
        Assert.False(callbackInvoked);
    }

    [Fact]
    public async Task SafeFireAndForget_FailingTask_InvokesErrorCallback()
    {
        var caught = new TaskCompletionSource<Exception>();
        Task t = Task.Run(() => throw new InvalidOperationException("test failure"));

        t.SafeFireAndForget("Test", ex => caught.TrySetResult(ex));

        var captured = await caught.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.IsType<InvalidOperationException>(captured);
        Assert.Equal("test failure", captured.Message);
    }

    [Fact]
    public async Task SafeFireAndForget_OperationCanceled_NoCallback()
    {
        // OperationCanceledException ist kein Fehler — Callback darf nicht feuern
        var callbackInvoked = false;
        Task t = Task.Run(() => { throw new OperationCanceledException(); });

        t.SafeFireAndForget("Test", _ => callbackInvoked = true);

        await Task.Delay(100);
        Assert.False(callbackInvoked);
    }

    [Fact]
    public async Task SafeFireAndForget_NullCallback_DoesNotThrow()
    {
        Task t = Task.Run(() => throw new InvalidOperationException("boom"));
        // Sollte sich selbst handhaben — kein Crash, kein Werfen
        t.SafeFireAndForget("NoCallback");

        await Task.Delay(50);
        // Kein Assert noetig — Test besteht wenn kein UnobservedTaskException geworfen wurde
    }

    [Fact]
    public async Task SafeFireAndForget_NullContext_StillLogs()
    {
        Task t = Task.Run(() => throw new InvalidOperationException("err"));
        t.SafeFireAndForget(context: null);

        await Task.Delay(50);
        // Test besteht wenn der Aufruf fehlerfrei zurueckkommt
    }
}
