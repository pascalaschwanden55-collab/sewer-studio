using AuswertungPro.Next.Application.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public class TrainingSampleStatusTests
{
    [Fact]
    public void Removed_IsAppendedAsValue3_ExistingValuesUnchanged()
    {
        Assert.Equal(0, (int)TrainingSampleStatus.New);
        Assert.Equal(1, (int)TrainingSampleStatus.Approved);
        Assert.Equal(2, (int)TrainingSampleStatus.Rejected);
        Assert.Equal(3, (int)TrainingSampleStatus.Removed);
    }
}
