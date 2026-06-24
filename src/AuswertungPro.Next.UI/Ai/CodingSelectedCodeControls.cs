using System;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingSelectedCodeControls
{
    public static void Clear(TextBlock selectedCodeText)
    {
        ArgumentNullException.ThrowIfNull(selectedCodeText);

        selectedCodeText.Text = "";
    }
}
