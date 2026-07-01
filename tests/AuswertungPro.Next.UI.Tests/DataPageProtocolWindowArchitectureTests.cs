using System;
using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageProtocolWindowArchitectureTests
{
    [Fact]
    public void DataPageViewModel_delegiert_protocol_window_an_controller()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DataPageViewModel.cs"));

        var method = ExtractMethodBody(source, "private void OpenProtocol(HaltungRecord? record)");

        Assert.Contains("_protocolWindowController.Open(record);", method, StringComparison.Ordinal);
        Assert.DoesNotContain("new AuswertungPro.Next.UI.Views.ProtocolObservationsWindow", method, StringComparison.Ordinal);
        Assert.DoesNotContain("Path.GetDirectoryName", method, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveExistingPath(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("SyncObservationsToHoldingFields(record)", method, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshSelectedProtocolEntries()", method, StringComparison.Ordinal);
    }

}
