using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingProtocolMatchArchitectureTests
{
    [Fact]
    public void PlayerWindow_import_confirmation_badge_uses_display_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var trainingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingProtocolMatchDisplayPolicy.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingProtocolImportTrainingWorkflowService.cs");

        var training = File.ReadAllText(trainingPath);
        var policy = File.ReadAllText(policyPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : string.Empty;

        Assert.Contains("CodingProtocolMatchDisplayPolicy.BuildImportConfirmationBadge", workflow);
        Assert.DoesNotContain("bestaetigt", training);
        Assert.DoesNotContain("Interval = TimeSpan.FromSeconds(3)", training);
        Assert.Contains("public static CodingImportConfirmationBadgeState BuildImportConfirmationBadge", policy);
    }

    [Fact]
    public void PlayerWindow_green_match_accept_overlay_uses_display_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var trainingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingProtocolMatchDisplayPolicy.cs");
        var runnerPath = Path.Combine(uiRoot, "Ai", "CodingProtocolGreenMatchTrainingRunner.cs");

        var training = File.ReadAllText(trainingPath);
        var policy = File.ReadAllText(policyPath);
        var runner = File.Exists(runnerPath) ? File.ReadAllText(runnerPath) : "";

        Assert.Contains("CodingProtocolGreenMatchTrainingRunner.AcceptGreenMatchesAsync", training);
        Assert.DoesNotContain("CodingProtocolMatchDisplayPolicy.BuildAcceptedGreenMatchesOverlay", training);
        Assert.Contains("CodingProtocolMatchDisplayPolicy.BuildAcceptedGreenMatchesOverlay", runner);
        Assert.DoesNotContain("gruene Treffer als Training uebernommen", training);
        Assert.DoesNotContain("ShowOverlay($\"{accepted}", training);
        Assert.Contains("public static CodingProtocolMatchOverlayState BuildAcceptedGreenMatchesOverlay", policy);
    }
}
