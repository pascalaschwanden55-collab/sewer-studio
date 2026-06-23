using System.Windows.Controls;
using System.Windows.Documents;

namespace AuswertungPro.Next.UI.Ai;

public sealed class CodingStatisticsControls
{
    private readonly Run _totalCount;
    private readonly Run _openCount;
    private readonly TextBlock _autoAccepted;
    private readonly TextBlock _pending;
    private readonly TextBlock _reviewRequired;
    private readonly TextBlock _averageConfidence;

    public CodingStatisticsControls(
        Run totalCount,
        Run openCount,
        TextBlock autoAccepted,
        TextBlock pending,
        TextBlock reviewRequired,
        TextBlock averageConfidence)
    {
        _totalCount = totalCount;
        _openCount = openCount;
        _autoAccepted = autoAccepted;
        _pending = pending;
        _reviewRequired = reviewRequired;
        _averageConfidence = averageConfidence;
    }

    public void Apply(CodingStatisticsSummary summary)
    {
        _totalCount.Text = summary.Total.ToString();
        _openCount.Text = summary.Open.ToString();
        _autoAccepted.Text = summary.AutoAccepted.ToString();
        _pending.Text = summary.Pending.ToString();
        _reviewRequired.Text = summary.ReviewRequired.ToString();
        _averageConfidence.Text = summary.AverageConfidenceText;
    }
}
