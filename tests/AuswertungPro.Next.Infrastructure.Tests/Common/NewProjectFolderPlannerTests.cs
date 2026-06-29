using System;
using System.Collections.Generic;
using System.IO;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Tests.Common;

public sealed class NewProjectFolderPlannerTests
{
    private static Func<string, bool> Existing(params string[] dirs)
    {
        var set = new HashSet<string>(dirs, StringComparer.OrdinalIgnoreCase);
        return set.Contains;
    }

    [Fact]
    public void Plan_builds_folder_and_projectfile_from_name()
    {
        var plan = NewProjectFolderPlanner.Plan(@"D:\Projekt", "Meiental_Husen", Existing());

        Assert.Equal(Path.Combine(@"D:\Projekt", "Meiental_Husen"), plan.FolderPath);
        Assert.Equal(Path.Combine(@"D:\Projekt", "Meiental_Husen", "projekt.json"), plan.ProjectFilePath);
    }

    [Fact]
    public void Plan_sanitizes_invalid_characters()
    {
        var plan = NewProjectFolderPlanner.Plan(@"D:\Projekt", "A/B:C", Existing());

        Assert.Equal(Path.Combine(@"D:\Projekt", "A_B_C"), plan.FolderPath);
    }

    [Fact]
    public void Plan_appends_suffix_on_collision()
    {
        var taken = Path.Combine(@"D:\Projekt", "Meiental_Husen");
        var plan = NewProjectFolderPlanner.Plan(@"D:\Projekt", "Meiental_Husen", Existing(taken));

        Assert.Equal(Path.Combine(@"D:\Projekt", "Meiental_Husen-2"), plan.FolderPath);
    }

    [Fact]
    public void Plan_increments_suffix_until_free()
    {
        var taken1 = Path.Combine(@"D:\Projekt", "Husen");
        var taken2 = Path.Combine(@"D:\Projekt", "Husen-2");
        var plan = NewProjectFolderPlanner.Plan(@"D:\Projekt", "Husen", Existing(taken1, taken2));

        Assert.Equal(Path.Combine(@"D:\Projekt", "Husen-3"), plan.FolderPath);
    }
}
