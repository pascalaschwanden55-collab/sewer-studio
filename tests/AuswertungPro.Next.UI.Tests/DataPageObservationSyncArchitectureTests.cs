using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageObservationSyncArchitectureTests
{
    [Fact]
    public void DataPageViewModel_delegiert_observation_sync_an_controller()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var viewModelSource = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DataPageViewModel.cs"));
        var controllerSource = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "DataPage", "DataPageObservationSyncController.cs"));

        Assert.Contains("private readonly DataPageObservationSyncController _observationSyncController;", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("new DataPageObservationSyncController(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("DataPageProtocolObservationMapper.Build", controllerSource, StringComparison.Ordinal);
        Assert.Contains("record.SetFieldValue(\"Primaere_Schaeden\"", controllerSource, StringComparison.Ordinal);

        var method = SourceTextTestHelpers.ExtractMethodBody(
            viewModelSource,
            "public void SyncObservationsToHoldingFields(HaltungRecord? record, bool showStatus = false)");

        Assert.Contains("_observationSyncController.Sync(record, showStatus);", method, StringComparison.Ordinal);
        Assert.DoesNotContain("DataPageProtocolObservationMapper.Build", method, StringComparison.Ordinal);
        Assert.DoesNotContain("record.SetFieldValue(\"Primaere_Schaeden\"", method, StringComparison.Ordinal);
        Assert.DoesNotContain("_shell.Project.Dirty = true", method, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshRecordInGrid(record)", method, StringComparison.Ordinal);
        Assert.DoesNotContain("ScheduleAutoSave()", method, StringComparison.Ordinal);
    }
}
