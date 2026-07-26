using System;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingAnalyzeButtonControls
{
    public static void SetEnabled(Button analyzeButton, bool isEnabled)
    {
        ArgumentNullException.ThrowIfNull(analyzeButton);

        analyzeButton.IsEnabled = isEnabled;
    }
}
