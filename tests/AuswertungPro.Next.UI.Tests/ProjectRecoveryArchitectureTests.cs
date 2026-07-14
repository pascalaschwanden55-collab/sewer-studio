using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjectRecoveryArchitectureTests
{
    [Fact]
    public void Shell_uses_central_recovery_instance_and_keeps_static_facade_thin()
    {
        var provider = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ServiceProvider.cs"));
        var shell = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "ShellViewModel.cs"));
        var facade = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.Infrastructure", "Projects", "ProjectRecovery.cs"));
        var service = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.Infrastructure", "Projects", "ProjectRecoveryService.cs"));

        Assert.Contains("public IProjectRecoveryService ProjectRecovery", provider);
        Assert.Contains("ProjectRecovery = new ProjectRecoveryService()", provider);
        Assert.Contains("_sp.ProjectRecovery.TryRecover(path, _sp.Projects)", shell);
        Assert.DoesNotContain("return (res, ProjectRecovery.TryRecover(", shell);
        Assert.Contains("private static readonly IProjectRecoveryService DefaultService", facade);
        Assert.DoesNotContain("File.Move", facade);
        Assert.Contains("public sealed class ProjectRecoveryService : IProjectRecoveryService", service);
        Assert.Contains("lock (_sync)", service);
        Assert.Contains("File.Move", service);
    }
}
