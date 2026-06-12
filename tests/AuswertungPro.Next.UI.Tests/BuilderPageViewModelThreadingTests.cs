using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class BuilderPageViewModelThreadingTests
{
    [Fact]
    public void RecordPropertyChanged_UsesDispatcherBeforeTouchingDebounceTimer()
    {
        var source = File.ReadAllText(FindRepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "BuilderPageViewModel.cs"));

        Assert.Contains("ScheduleRefreshDataOnUiThread", source);
        Assert.Contains(".Dispatcher.CheckAccess()", source);
        Assert.Contains(".Dispatcher.BeginInvoke", source);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("Repo-Datei nicht gefunden.", Path.Combine(relativeParts));
    }
}
