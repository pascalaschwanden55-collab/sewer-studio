using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataGridEditedTextValueResolverArchitectureTests
{
    [Fact]
    public void DataPage_uses_shared_edited_text_resolver()
    {
        var root = TestRepoPaths.FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml.cs"));

        Assert.Contains("DataGridEditedTextValueResolver.Resolve(", source, StringComparison.Ordinal);
        Assert.Contains("DataGridEditedTextValueResolver.ResolveComboBoxValue(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string? GetEditedTextValue", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string ResolveComboBoxValue", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SchaechtePage_uses_shared_edited_text_resolver()
    {
        var root = TestRepoPaths.FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Pages", "SchaechtePage.xaml.cs"));

        Assert.Contains("DataGridEditedTextValueResolver.TryResolve(", source, StringComparison.Ordinal);
        Assert.Contains("DataGridEditedTextValueResolver.ResolveComboBoxValue(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool TryGetEditedTextValue", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string ResolveComboBoxValue", source, StringComparison.Ordinal);
    }
}
