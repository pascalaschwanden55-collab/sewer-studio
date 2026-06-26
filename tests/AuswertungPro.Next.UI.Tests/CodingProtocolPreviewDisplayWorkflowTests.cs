using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using UiServiceProvider = AuswertungPro.Next.UI.ServiceProvider;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolPreviewDisplayWorkflowTests
{
    [Fact]
    public void TryShow_creates_preview_service_and_delegates_request()
    {
        var calls = new List<string>();
        var record = new HaltungRecord();
        var document = new ProtocolDocument();
        document.Current.Entries.Add(new ProtocolEntry { Code = "BAJ" });
        var project = new Project();
        var service = new CodingProtocolPreviewWorkflowService(
            confirmPreview: count =>
            {
                calls.Add($"confirm:{count}");
                return true;
            },
            getCurrentProject: () => project,
            resolveProjectFolder: path =>
            {
                calls.Add($"folder:{path}");
                return @"C:\project";
            },
            showPreviewWindow: (owner, actualRecord, actualProject, serviceProvider, videoPath, projectFolder, markDirty) =>
            {
                Assert.Null(owner);
                Assert.Same(record, actualRecord);
                Assert.Same(project, actualProject);
                Assert.Null(serviceProvider);
                Assert.Equal("video.mp4", videoPath);
                Assert.Equal(@"C:\project", projectFolder);
                markDirty();
                calls.Add("show");
            });

        var opened = CodingProtocolPreviewDisplayWorkflow.TryShow(
            owner: null,
            record,
            document,
            serviceProvider: null!,
            videoPath: "video.mp4",
            lastProjectPath: @"C:\project\project.json",
            markDirty: () => calls.Add("dirty"),
            new CodingProtocolPreviewDisplayWorkflowActions(
                CreateService: () =>
                {
                    calls.Add("service");
                    return service;
                }));

        Assert.True(opened);
        Assert.Equal(["service", "confirm:1", @"folder:C:\project\project.json", "dirty", "show"], calls);
    }
}
