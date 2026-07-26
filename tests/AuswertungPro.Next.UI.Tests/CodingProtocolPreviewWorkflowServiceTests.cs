using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using UiServiceProvider = AuswertungPro.Next.UI.ServiceProvider;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolPreviewWorkflowServiceTests
{
    [Fact]
    public void TryShow_confirms_gets_project_resolves_folder_and_shows_preview()
    {
        var record = new HaltungRecord();
        var doc = BuildDocument(entryCount: 3);
        var project = new Project();
        var markDirtyCalled = false;
        var shown = new List<(HaltungRecord Record, Project Project, string? VideoPath, string? ProjectFolder)>();
        var service = new CodingProtocolPreviewWorkflowService(
            confirmPreview: count => count == 3,
            getCurrentProject: () => project,
            resolveProjectFolder: projectPath =>
            {
                Assert.Equal(@"C:\project\project.json", projectPath);
                return @"C:\project";
            },
            showPreviewWindow: (_, actualRecord, actualProject, _, videoPath, projectFolder, markDirty) =>
            {
                markDirty();
                shown.Add((actualRecord, actualProject, videoPath, projectFolder));
            });

        var opened = service.TryShow(
            owner: null,
            record,
            doc,
            serviceProvider: null!,
            videoPath: "video.mp4",
            lastProjectPath: @"C:\project\project.json",
            markDirty: () => markDirtyCalled = true);

        Assert.True(opened);
        Assert.True(markDirtyCalled);
        var preview = Assert.Single(shown);
        Assert.Same(record, preview.Record);
        Assert.Same(project, preview.Project);
        Assert.Equal("video.mp4", preview.VideoPath);
        Assert.Equal(@"C:\project", preview.ProjectFolder);
    }

    [Fact]
    public void TryShow_returns_false_without_project_or_window_when_confirmation_is_declined()
    {
        var service = new CodingProtocolPreviewWorkflowService(
            confirmPreview: _ => false,
            getCurrentProject: () => throw new InvalidOperationException("Project must not be read."),
            resolveProjectFolder: _ => throw new InvalidOperationException("Project folder must not be resolved."),
            showPreviewWindow: (_, _, _, _, _, _, _) => throw new InvalidOperationException("Window must not open."));

        var opened = service.TryShow(
            owner: null,
            new HaltungRecord(),
            BuildDocument(entryCount: 1),
            serviceProvider: null!,
            videoPath: null,
            lastProjectPath: null,
            markDirty: () => { });

        Assert.False(opened);
    }

    [Fact]
    public void TryShow_returns_false_when_project_is_missing()
    {
        var service = new CodingProtocolPreviewWorkflowService(
            confirmPreview: _ => true,
            getCurrentProject: () => null,
            resolveProjectFolder: _ => throw new InvalidOperationException("Project folder must not be resolved."),
            showPreviewWindow: (_, _, _, _, _, _, _) => throw new InvalidOperationException("Window must not open."));

        var opened = service.TryShow(
            owner: null,
            new HaltungRecord(),
            BuildDocument(entryCount: 1),
            serviceProvider: null!,
            videoPath: null,
            lastProjectPath: null,
            markDirty: () => { });

        Assert.False(opened);
    }

    [Fact]
    public void Factory_creates_workflow_service()
    {
        var service = CodingProtocolPreviewWorkflowServiceFactory.Create();

        Assert.NotNull(service);
    }

    private static ProtocolDocument BuildDocument(int entryCount)
    {
        var doc = new ProtocolDocument();
        for (var i = 0; i < entryCount; i++)
        {
            doc.Current.Entries.Add(new ProtocolEntry { Code = $"C{i}" });
        }

        return doc;
    }
}
