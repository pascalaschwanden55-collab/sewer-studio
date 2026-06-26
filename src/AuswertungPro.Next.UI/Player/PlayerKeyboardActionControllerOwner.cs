namespace AuswertungPro.Next.UI.Player;

public sealed class PlayerKeyboardActionControllerOwner
{
    private readonly Func<PlayerKeyboardActionControllerFactoryActions, PlayerKeyboardActionController> _createController;
    private PlayerKeyboardActionController? _controller;

    public PlayerKeyboardActionControllerOwner()
        : this(PlayerKeyboardActionControllerFactory.Create)
    {
    }

    public PlayerKeyboardActionControllerOwner(
        Func<PlayerKeyboardActionControllerFactoryActions, PlayerKeyboardActionController> createController)
    {
        ArgumentNullException.ThrowIfNull(createController);

        _createController = createController;
    }

    public PlayerKeyboardActionController Ensure(PlayerKeyboardActionControllerFactoryActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        return _controller ??= _createController(actions);
    }
}
