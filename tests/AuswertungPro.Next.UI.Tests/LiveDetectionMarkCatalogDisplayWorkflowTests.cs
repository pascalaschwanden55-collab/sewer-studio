using System.Windows;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionMarkCatalogDisplayWorkflowTests
{
    [Fact]
    public void TryOpen_creates_service_and_delegates_catalog_open_request()
    {
        var calls = new List<string>();
        var selected = new ProtocolEntry
        {
            Code = "BAG",
            Beschreibung = "Versatz",
            MeterStart = 7.4
        };

        var opened = LiveDetectionMarkCatalogDisplayWorkflow.TryOpen(
            new LiveDetectionMarkCatalogDisplayRequest(
                ClockPosition: "03:00",
                TimestampSec: 12.5,
                SuggestedCode: "BAJ",
                Meter: 7.2,
                VideoPath: "video.mp4",
                Owner: null!),
            new LiveDetectionMarkCatalogDisplayActions(
                HasCodeCatalog: () =>
                {
                    calls.Add("has-catalog");
                    return true;
                },
                CreateViewModel: (entry, meter, videoTime) =>
                {
                    calls.Add($"vm:{entry.Code}:{meter:F1}:{videoTime!.Value.TotalSeconds:F1}");
                    return null!;
                },
                OnEntryCreated: entry => calls.Add($"created:{entry.Code}:{entry.MeterStart:F1}"),
                ShowOverlay: message => calls.Add($"overlay:{message}")),
            createService: (hasCodeCatalog, createViewModel, onEntryCreated, showOverlay) =>
            {
                calls.Add("service");
                return new LiveDetectionMarkCatalogWorkflowService(
                    hasCodeCatalog,
                    showCodeCatalogUnavailable: () => throw new InvalidOperationException("Catalog must be available."),
                    createViewModel,
                    showDialog: (viewModel, videoPath, videoTime, owner) =>
                    {
                        Assert.Null(viewModel);
                        Assert.Equal("video.mp4", videoPath);
                        Assert.Equal(TimeSpan.FromSeconds(12.5), videoTime);
                        Assert.Null(owner);
                        calls.Add("dialog");
                        return new VsaCodeExplorerDialogResult(true, selected);
                    },
                    onEntryCreated,
                    showOverlay);
            });

        Assert.True(opened);
        Assert.Equal(
            [
                "service",
                "has-catalog",
                "vm:BAJ:7.2:12.5",
                "dialog",
                "created:BAG:7.4",
                "overlay:Beobachtung erfasst: BAG"
            ],
            calls);
    }
}
