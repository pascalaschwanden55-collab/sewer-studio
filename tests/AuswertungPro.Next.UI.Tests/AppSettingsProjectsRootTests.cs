using System.Text.Json;
using AuswertungPro.Next.UI;

namespace AuswertungPro.Next.UI.Tests;

public sealed class AppSettingsProjectsRootTests
{
    [Fact]
    public void ProjectsRootDirectory_survives_json_roundtrip()
    {
        var settings = new AppSettings { ProjectsRootDirectory = @"D:\Projekt" };

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(restored);
        Assert.Equal(@"D:\Projekt", restored!.ProjectsRootDirectory);
    }

    [Fact]
    public void ProjectsRootDirectory_defaults_to_null()
        => Assert.Null(new AppSettings().ProjectsRootDirectory);
}
