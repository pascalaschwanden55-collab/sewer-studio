using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed class SelfTrainingMatchRateTracker
{
    private int _exact;
    private int _partial;
    private int _mismatch;
    private int _noFindings;

    public void Record(MatchLevel level)
    {
        switch (level)
        {
            case MatchLevel.ExactMatch:
                _exact++;
                break;
            case MatchLevel.PartialMatch:
                _partial++;
                break;
            case MatchLevel.Mismatch:
                _mismatch++;
                break;
            case MatchLevel.NoFindings:
                _noFindings++;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(level), level, null);
        }
    }

    public void Reset()
    {
        _exact = 0;
        _partial = 0;
        _mismatch = 0;
        _noFindings = 0;
    }

    public SelfTrainingStatusCalculator.MatchRatePercents ComputePercents()
        => SelfTrainingStatusCalculator.ComputeMatchRatePercents(
            _exact,
            _partial,
            _mismatch,
            _noFindings);
}
