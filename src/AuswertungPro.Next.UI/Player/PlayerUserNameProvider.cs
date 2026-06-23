namespace AuswertungPro.Next.UI.Player;

public static class PlayerUserNameProvider
{
    public static string Current(Func<string>? userNameProvider = null)
        => (userNameProvider ?? (() => Environment.UserName))();
}
