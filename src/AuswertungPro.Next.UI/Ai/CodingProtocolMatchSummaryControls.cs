using System;
using System.Windows.Controls;
using AuswertungPro.Next.Application.Ai.Evaluation;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingProtocolMatchSummaryControls
{
    public static void Apply(
        TextBlock summaryText,
        Button acceptGreenMatchesButton,
        CodingMatchRouting? routing)
    {
        ArgumentNullException.ThrowIfNull(summaryText);
        ArgumentNullException.ThrowIfNull(acceptGreenMatchesButton);

        summaryText.Text = CodingProtocolMatchSummaryFormatter.Format(routing);
        acceptGreenMatchesButton.IsEnabled = CodingProtocolMatchSummaryFormatter.CanAcceptGreenMatches(routing);
    }
}
