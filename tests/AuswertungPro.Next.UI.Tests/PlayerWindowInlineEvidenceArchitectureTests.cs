using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowInlineEvidenceArchitectureTests
{
    [Fact]
    public void PlayerWindow_inline_evidence_preview_uses_service()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var previewPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.EventDetails.Preview.cs");
        var servicePath = Path.Combine(uiRoot, "Ai", "CodingInlineEvidencePreviewService.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingInlineEvidencePreviewWorkflow.cs");

        Assert.True(File.Exists(servicePath), "Inline-Beweisbild-Vorschau soll Datei- und Bitmap-Logik ausserhalb der PlayerWindow-Partials halten.");
        Assert.True(File.Exists(workflowPath), "Inline-Beweisbild-Vorschau-Fehlerbehandlung soll ausserhalb der PlayerWindow-Partials liegen.");

        var preview = File.ReadAllText(previewPath);
        var service = File.ReadAllText(servicePath);
        var workflow = File.ReadAllText(workflowPath);

        Assert.Contains("CodingInlineEvidencePreviewWorkflow.Execute", preview);
        Assert.DoesNotContain("CodingInlineEvidencePreviewService.Build", preview);
        Assert.DoesNotContain("catch (Exception", preview);
        Assert.Contains("CodingInlineEvidencePreviewService.Build", workflow);
        Assert.Contains("CodingInlineEvidencePreviewService.LoadFailed", workflow);
        Assert.DoesNotContain("File.Exists", preview);
        Assert.DoesNotContain("new BitmapImage", preview);
        Assert.Contains("File.Exists", service);
        Assert.Contains("new BitmapImage", service);
    }
}
