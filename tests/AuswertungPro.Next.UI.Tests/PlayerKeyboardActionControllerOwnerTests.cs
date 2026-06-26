using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerKeyboardActionControllerOwnerTests
{
    [Fact]
    public void Ensure_creates_controller_lazily_and_reuses_it()
    {
        var createCount = 0;
        var controller = CreateController();
        var owner = new PlayerKeyboardActionControllerOwner(_ =>
        {
            createCount++;
            return controller;
        });
        var actions = CreateActions();

        var first = owner.Ensure(actions);
        var second = owner.Ensure(actions);

        Assert.Same(controller, first);
        Assert.Same(first, second);
        Assert.Equal(1, createCount);
    }

    [Fact]
    public void Constructor_throws_for_null_factory()
    {
        Assert.Throws<ArgumentNullException>(() => new PlayerKeyboardActionControllerOwner(null!));
    }

    [Fact]
    public void Ensure_throws_for_null_actions()
    {
        var owner = new PlayerKeyboardActionControllerOwner(_ => CreateController());
        void EnsureWithNullActions()
        {
            owner.Ensure(null!);
        }

        Assert.Throws<ArgumentNullException>((Action)EnsureWithNullActions);
    }

    private static PlayerKeyboardActionControllerFactoryActions CreateActions()
        => new(
            CancelCodingOverlay: () => { },
            TogglePlayPause: () => { },
            StopPlayback: () => { },
            SetPause: _ => { },
            EnsurePlaying: () => { },
            ChangeSpeed: _ => { },
            JumpSeconds: _ => { },
            ToggleDetection: () => { },
            ToggleMarkTool: () => { });

    private static PlayerKeyboardActionController CreateController()
        => new(new PlayerKeyboardActionBindings
        {
            CancelCodingOverlay = () => { },
            TogglePlayPause = () => { },
            Stop = () => { },
            Pause = () => { },
            Resume = () => { },
            ChangeSpeed = _ => { },
            JumpSeconds = _ => { },
            ToggleDetection = () => { },
            ToggleMarkTool = () => { }
        });
}
