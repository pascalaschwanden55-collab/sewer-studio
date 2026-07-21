using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ActiveProjectGuardTests
{
    [Fact]
    public void IsCurrent_accepts_same_project_and_normalized_location()
    {
        var project = new Project();

        Assert.True(ActiveProjectGuard.IsCurrent(
            new ProjectOperationContext(project, " C:\\Projekt\\projekt.json "),
            project,
            "C:\\Projekt\\projekt.json"));
    }

    [Fact]
    public void IsCurrent_rejects_other_project_instance_at_same_location()
    {
        var expected = new Project();
        var replacement = new Project { Id = expected.Id };

        Assert.False(ActiveProjectGuard.IsCurrent(
            new ProjectOperationContext(expected, "C:\\Projekt\\projekt.json"),
            replacement,
            "C:\\Projekt\\projekt.json"));
    }

    [Fact]
    public void IsCurrent_rejects_same_project_at_other_location()
    {
        var project = new Project();

        Assert.False(ActiveProjectGuard.IsCurrent(
            new ProjectOperationContext(project, "C:\\Projekt\\a.json"),
            project,
            "C:\\Projekt\\b.json"));
    }

    [Fact]
    public void IsCurrent_resolves_parent_segments_and_ignores_path_casing()
    {
        var project = new Project();

        Assert.True(ActiveProjectGuard.IsCurrent(
            new ProjectOperationContext(
                project,
                "C:\\PROJEKT\\Unterordner\\..\\A.JSON"),
            project,
            "c:\\projekt\\a.json"));
    }
}
