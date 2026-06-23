using System.Diagnostics;

namespace AuswertungPro.Next.UI.Player;

public static class PlayerTrace
{
    public static void WriteLine(string message, Action<string>? writeLine = null)
    {
        if (writeLine is not null)
        {
            writeLine(message);
            return;
        }

        Debug.WriteLine(message);
    }
}
