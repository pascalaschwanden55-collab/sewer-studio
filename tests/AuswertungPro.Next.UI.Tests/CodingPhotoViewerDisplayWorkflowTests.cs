using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingPhotoViewerDisplayWorkflowTests
{
    [Fact]
    public void Show_creates_viewer_service_and_delegates_request()
    {
        var calls = new List<string>();
        var codingEvent = new CodingEvent { Entry = new ProtocolEntry { Code = "BAJ" } };
        var service = new CodingPhotoViewerWorkflowService(
            resolveProjectFolder: lastProjectPath =>
            {
                Assert.Equal(@"C:\project\project.json", lastProjectPath);
                calls.Add("folder");
                return @"C:\project";
            },
            showViewer: (owner, actualEvent, projectFolder) =>
            {
                Assert.Null(owner);
                Assert.Same(codingEvent, actualEvent);
                calls.Add($"show:{projectFolder}");
            });

        CodingPhotoViewerDisplayWorkflow.Show(
            owner: null!,
            codingEvent,
            @"C:\project\project.json",
            new CodingPhotoViewerDisplayWorkflowActions(
                CreateService: () =>
                {
                    calls.Add("service");
                    return service;
                }));

        Assert.Equal(["service", "folder", @"show:C:\project"], calls);
    }
}
