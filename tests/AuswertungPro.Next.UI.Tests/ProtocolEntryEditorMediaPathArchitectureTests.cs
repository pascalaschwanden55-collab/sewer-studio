using System.IO;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProtocolEntryEditorMediaPathArchitectureTests
{
    [Fact]
    public void Protocol_entry_editor_keeps_media_file_resolution_outside_the_window()
    {
        var dialogPath = RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "ProtocolEntryEditorDialog.xaml.cs");
        var resolverPath = RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "ProtocolEntryEditorMediaPathResolver.cs");

        Assert.True(File.Exists(resolverPath), "Medienpfade des Protokoll-Editors brauchen einen eigenen Resolver.");

        var dialog = File.ReadAllText(dialogPath);
        var resolver = File.ReadAllText(resolverPath);
        var compactDialog = string.Concat(dialog.Where(character => !char.IsWhiteSpace(character)));

        Assert.Contains("internal sealed class ProtocolEntryEditorMediaPathResolver", resolver);
        Assert.DoesNotContain("System.Windows", resolver);
        Assert.DoesNotContain("ServiceProvider", resolver);
        Assert.Contains("ProjectFileLocator.ProjectRootFromFile", resolver);
        Assert.Contains("_currentProjectPath()", resolver);
        Assert.Contains("File.Exists", resolver);
        Assert.Contains("ProtocolEntryEditorMediaPathResolver", dialog);
        Assert.Contains("new ProtocolEntryEditorMediaPathResolver", dialog);
        Assert.Contains("Settings.LastProjectPath", dialog);
        var projectResolution = Regex.Match(
            compactDialog,
            @"ProjectFolderAbs:(?<resolver>[A-Za-z_]\w*)\.ResolveProjectFolder\(\)");
        var videoResolution = Regex.Match(
            compactDialog,
            @"VideoPathAbs:(?<resolver>[A-Za-z_]\w*)\.ResolveExistingPath\(_videoPath\)");
        var imageResolution = Regex.Match(
            compactDialog,
            @"ImagePathsAbs:(?<resolver>[A-Za-z_]\w*)\.ResolveImagePaths\(_entryVm\.Model\.FotoPaths\)");
        Assert.True(projectResolution.Success);
        Assert.True(videoResolution.Success);
        Assert.True(imageResolution.Success);
        Assert.Equal(
            projectResolution.Groups["resolver"].Value,
            videoResolution.Groups["resolver"].Value);
        Assert.Equal(
            projectResolution.Groups["resolver"].Value,
            imageResolution.Groups["resolver"].Value);
        Assert.DoesNotContain("private string ResolveProjectFolder", dialog);
        Assert.DoesNotContain("private string? ResolveExistingPath", dialog);
        Assert.DoesNotContain("private IReadOnlyList<string> ResolveImagePaths", dialog);
        Assert.DoesNotContain("File.Exists(", dialog);
        Assert.DoesNotContain("Path.IsPathRooted(", dialog);
        Assert.DoesNotContain("Path.GetFullPath(", dialog);
        Assert.DoesNotContain("Path.GetDirectoryName(", dialog);
        var requestIndex = compactDialog.IndexOf(
            "varrequest=newProtocolEntryKiSuggestionRequest(",
            StringComparison.Ordinal);
        var busyIndex = compactDialog.IndexOf("_isKiBusy=true;", StringComparison.Ordinal);
        Assert.True(
            requestIndex >= 0
            && requestIndex < projectResolution.Index
            && projectResolution.Index < videoResolution.Index
            && videoResolution.Index < imageResolution.Index
            && imageResolution.Index < busyIndex);
    }
}
