using System.IO;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerWindowDispatcherTests
{
    [Fact]
    public void View_model_property_changes_are_dispatched_without_blocking_caller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "VsaCodeExplorerWindow.xaml.cs"));

        Assert.DoesNotContain("Dispatcher.Invoke(", source);
        Assert.Contains("Dispatcher.CheckAccess()", source);
        Assert.Contains("Dispatcher.BeginInvoke", source);
        Assert.Contains("ApplyViewModelPropertyChanged", source);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AuswertungPro.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
