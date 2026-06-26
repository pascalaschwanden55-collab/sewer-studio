using System.Windows.Controls.Primitives;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingLiveAiTimerControllerOwnerTests
{
    [Fact]
    public void Ensure_creates_controller_once_and_marks_owner_initialized()
    {
        RunOnStaThread(() =>
        {
            var owner = new CodingLiveAiTimerControllerOwner();
            var createCount = 0;

            var first = owner.Ensure(() =>
            {
                createCount++;
                return CreateController();
            });
            var second = owner.Ensure(() =>
            {
                createCount++;
                return CreateController();
            });

            Assert.True(owner.HasController);
            Assert.Same(first, second);
            Assert.Same(first, owner.Controller);
            Assert.Equal(1, createCount);
        });
    }

    [Fact]
    public void Stop_allows_missing_controller()
    {
        var owner = new CodingLiveAiTimerControllerOwner();

        owner.Stop(resetButton: true);

        Assert.False(owner.HasController);
    }

    private static CodingLiveAiTimerController CreateController()
        => new(new ToggleButton(), (_, _) => { }, () => true);

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
