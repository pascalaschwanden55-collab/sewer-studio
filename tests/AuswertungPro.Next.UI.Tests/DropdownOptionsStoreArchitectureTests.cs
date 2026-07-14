using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DropdownOptionsStoreArchitectureTests
{
    [Fact]
    public void ViewModels_nutzen_den_injizierten_Dropdown_Speicher()
    {
        var root = TestRepoPaths.FindRepoRoot();
        var pages = Path.Combine(root, "src", "AuswertungPro.Next.UI", "ViewModels", "Pages");
        var files = new[]
        {
            "DataPageViewModel.cs",
            "DataPageViewModel.DropdownOptions.cs",
            "ProjectPageViewModel.cs",
            "SchaechtePageViewModel.cs"
        };
        var source = string.Join(
            Environment.NewLine,
            files.Select(file => File.ReadAllText(Path.Combine(pages, file))));

        Assert.DoesNotContain("DropdownOptionsStore.", source, StringComparison.Ordinal);
        Assert.Contains("IDropdownOptionsStore", source, StringComparison.Ordinal);
        Assert.Contains("services.DropdownOptions", source, StringComparison.Ordinal);

        var provider = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "ServiceProvider.cs"));
        Assert.Contains("public IDropdownOptionsStore DropdownOptions { get; }", provider, StringComparison.Ordinal);
        Assert.Contains("DropdownOptions = new FileDropdownOptionsStore();", provider, StringComparison.Ordinal);
    }
}
