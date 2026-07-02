using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class BuilderPageViewModelThreadingTests
{
    [Fact]
    public void RecordPropertyChanged_UsesDispatcherBeforeTouchingDebounceTimer()
    {
        var source = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "BuilderPageViewModel.cs"));

        Assert.Contains("ScheduleRefreshDataOnUiThread", source);
        Assert.Contains(".Dispatcher.CheckAccess()", source);
        Assert.Contains(".Dispatcher.BeginInvoke", source);
    }

}
