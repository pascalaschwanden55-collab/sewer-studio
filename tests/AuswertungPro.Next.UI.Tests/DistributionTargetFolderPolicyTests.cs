using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DistributionTargetFolderPolicyTests
{
    [Fact]
    public void Resolve_uses_project_folder_without_prompting()
    {
        var prompted = false;

        var result = DistributionTargetFolderPolicy.Resolve(
            @"D:\Projekt\Kanal",
            () =>
            {
                prompted = true;
                return @"C:\Fallback";
            });

        Assert.Equal(@"D:\Projekt\Kanal", result);
        Assert.False(prompted);
    }

    [Fact]
    public void Resolve_falls_back_to_dialog_when_project_folder_missing()
    {
        var result = DistributionTargetFolderPolicy.Resolve(
            " ",
            () => @"C:\Fallback");

        Assert.Equal(@"C:\Fallback", result);
    }
}
