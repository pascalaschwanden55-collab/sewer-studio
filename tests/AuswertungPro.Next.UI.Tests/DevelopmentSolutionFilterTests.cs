using System.IO;
using System.Text.Json;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DevelopmentSolutionFilterTests
{
    [Fact]
    public void Alltagsfilter_enthaelt_nur_Produktcode_und_Tests()
    {
        var path = TestRepoPaths.RepoFile("AuswertungPro.Dev.slnf");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var projects = document.RootElement
            .GetProperty("solution")
            .GetProperty("projects")
            .EnumerateArray()
            .Select(element => element.GetString())
            .Order(StringComparer.Ordinal)
            .ToArray();

        var expected = new[]
        {
            @"src\AuswertungPro.Next.Application\AuswertungPro.Next.Application.csproj",
            @"src\AuswertungPro.Next.Domain\AuswertungPro.Next.Domain.csproj",
            @"src\AuswertungPro.Next.Infrastructure\AuswertungPro.Next.Infrastructure.csproj",
            @"src\AuswertungPro.Next.UI\AuswertungPro.Next.UI.csproj",
            @"tests\AuswertungPro.Next.Infrastructure.Tests\AuswertungPro.Next.Infrastructure.Tests.csproj",
            @"tests\AuswertungPro.Next.Pipeline.Tests\AuswertungPro.Next.Pipeline.Tests.csproj",
            @"tests\AuswertungPro.Next.UI.Tests\AuswertungPro.Next.UI.Tests.csproj",
            @"tests\ProjectModernizer.Tests\ProjectModernizer.Tests.csproj"
        }.Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(@"AuswertungPro.sln", document.RootElement
            .GetProperty("solution")
            .GetProperty("path")
            .GetString());
        Assert.Equal(expected, projects);
        Assert.DoesNotContain(projects, project =>
            project?.StartsWith(@"tools\", StringComparison.OrdinalIgnoreCase) == true);
    }
}
