using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class SelfTrainingFramePositionPolicyTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    public void IsReliable_matches_self_training_source_type_semantics(
        bool usedVideoFallback,
        bool hasProtocolTimestamp,
        bool expected)
    {
        Assert.Equal(expected,
            SelfTrainingFramePositionPolicy.IsReliable(usedVideoFallback, hasProtocolTimestamp));
    }
}
