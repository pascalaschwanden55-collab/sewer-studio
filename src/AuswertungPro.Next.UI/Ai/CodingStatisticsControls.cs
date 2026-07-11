using System.Windows.Controls;
using System.Windows.Documents;

namespace AuswertungPro.Next.UI.Ai;

public sealed class CodingStatisticsControls
{
    private readonly Run _totalCount;
    private readonly Run _openCount;
    private readonly TextBlock _aiCriteriaMet;
    private readonly TextBlock _humanAccepted;
    private readonly TextBlock _humanCorrected;
    private readonly TextBlock _rejected;
    private readonly TextBlock _open;
    private readonly TextBlock _averageAiConfidence;

    public CodingStatisticsControls(
        Run totalCount,
        Run openCount,
        TextBlock aiCriteriaMet,
        TextBlock humanAccepted,
        TextBlock humanCorrected,
        TextBlock rejected,
        TextBlock open,
        TextBlock averageAiConfidence)
    {
        _totalCount = totalCount;
        _openCount = openCount;
        _aiCriteriaMet = aiCriteriaMet;
        _humanAccepted = humanAccepted;
        _humanCorrected = humanCorrected;
        _rejected = rejected;
        _open = open;
        _averageAiConfidence = averageAiConfidence;
    }

    public void Apply(CodingStatisticsSummary summary)
    {
        _totalCount.Text = summary.Total.ToString();
        _openCount.Text = summary.Open.ToString();
        _aiCriteriaMet.Text = summary.AiCriteriaMet.ToString();
        _humanAccepted.Text = summary.HumanAccepted.ToString();
        _humanCorrected.Text = summary.HumanCorrected.ToString();
        _rejected.Text = summary.Rejected.ToString();
        _open.Text = summary.Open.ToString();
        _averageAiConfidence.Text = summary.AverageAiConfidenceText;
    }
}
