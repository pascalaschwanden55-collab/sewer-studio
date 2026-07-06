using Xunit;

public sealed class ProjectModernizerExitCodeResolverTests
{
    [Fact]
    public void ResolveReturnsSuccessWhenReportHasNoProblems()
    {
        var exitCode = ProjectModernizerExitCodeResolver.Resolve(new ModernizeReport());

        Assert.Equal(ProjectModernizerExitCodes.Success, exitCode);
    }

    [Fact]
    public void ResolveReturnsUnresolvedPathsWhenOnlyPathsAreMissing()
    {
        var exitCode = ProjectModernizerExitCodeResolver.Resolve(new ModernizeReport
        {
            UnresolvedPaths = 1
        });

        Assert.Equal(ProjectModernizerExitCodes.UnresolvedPaths, exitCode);
    }

    [Fact]
    public void ResolveReturnsCopyErrorsBeforeUnresolvedPaths()
    {
        var exitCode = ProjectModernizerExitCodeResolver.Resolve(new ModernizeReport
        {
            CopyErrors = 1,
            UnresolvedPaths = 1
        });

        Assert.Equal(ProjectModernizerExitCodes.CopyErrors, exitCode);
    }
}
