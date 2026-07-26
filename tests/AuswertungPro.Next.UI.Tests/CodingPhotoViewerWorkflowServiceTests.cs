using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingPhotoViewerWorkflowServiceTests
{
    [Fact]
    public void Show_resolves_project_folder_and_shows_viewer()
    {
        var codingEvent = new CodingEvent { Entry = new ProtocolEntry { Code = "BAJ" } };
        var shown = new List<(CodingEvent Event, string ProjectFolder)>();
        var service = new CodingPhotoViewerWorkflowService(
            resolveProjectFolder: projectPath =>
            {
                Assert.Equal(@"C:\project\project.json", projectPath);
                return @"C:\project";
            },
            showViewer: (_, actualEvent, projectFolder) =>
            {
                shown.Add((actualEvent, projectFolder));
            });

        service.Show(owner: null!, codingEvent, @"C:\project\project.json");

        var call = Assert.Single(shown);
        Assert.Same(codingEvent, call.Event);
        Assert.Equal(@"C:\project", call.ProjectFolder);
    }

    [Fact]
    public void Factory_creates_workflow_service()
    {
        var service = CodingPhotoViewerWorkflowServiceFactory.Create();

        Assert.NotNull(service);
    }
}
