using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowStretchDamageArchitectureTests
{
    [Fact]
    public void PlayerWindow_open_stretch_damage_prompt_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var boundariesPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Boundaries.cs");
        var closePromptPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Streckenschaden.ClosePrompt.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamagePromptBuilder.cs");
        var closePolicyPath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamagePolicy.cs");
        var closeApplierPath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamageCloseApplier.cs");
        var dialogServicePath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamageDialogService.cs");
        var dialogServiceFactoryPath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamageDialogServiceFactory.cs");
        var dialogWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamageDialogWorkflow.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamagePromptCommandWorkflow.cs");

        Assert.True(File.Exists(closePromptPath), "Dialog fuer offene Streckenschaeden soll aus dem Boundary-Partial heraus.");
        Assert.True(File.Exists(policyPath), "Dialogtext fuer offene Streckenschaeden muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(closePolicyPath), "Filter- und Schliessmeterlogik fuer offene Streckenschaeden muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(closeApplierPath), "Schliessanwendung fuer offene Streckenschaeden muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogServicePath), "Dialogentscheidung fuer offene Streckenschaeden muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogServiceFactoryPath), "DialogHost-Verdrahtung fuer offene Streckenschaeden muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogWorkflowPath), "Dialogaufruf fuer offene Streckenschaeden soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(commandWorkflowPath), "Offene-Streckenschaden-Dialogfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var boundaries = File.ReadAllText(boundariesPath);
        var closePrompt = File.ReadAllText(closePromptPath);
        var policy = File.ReadAllText(policyPath);
        var closePolicy = File.ReadAllText(closePolicyPath);
        var closeApplier = File.ReadAllText(closeApplierPath);
        var dialogService = File.ReadAllText(dialogServicePath);
        var dialogServiceFactory = File.ReadAllText(dialogServiceFactoryPath);
        var dialogWorkflow = File.Exists(dialogWorkflowPath) ? File.ReadAllText(dialogWorkflowPath) : "";
        var commandWorkflow = File.ReadAllText(commandWorkflowPath);

        Assert.DoesNotContain("private bool CloseOpenStreckenschaeden", boundaries);
        Assert.Contains("private bool CloseOpenStreckenschaeden", closePrompt);
        Assert.Contains("CodingOpenStretchDamagePromptCommandWorkflow.Execute", closePrompt);
        Assert.Contains("CodingOpenStretchDamageDialogWorkflow.ConfirmClose", closePrompt);
        Assert.DoesNotContain("CodingOpenStretchDamageDialogServiceFactory.Create", closePrompt);
        Assert.DoesNotContain("new CodingOpenStretchDamageDialogWorkflowActions", closePrompt);
        Assert.Contains("CodingOpenStretchDamagePolicy.FindOpen", closePrompt);
        Assert.Contains("CodingOpenStretchDamageCloseApplier.Apply", closePrompt);
        Assert.Contains("_codingSessionHost", closePrompt);
        Assert.DoesNotContain("_codingVm", closePrompt);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel) return true", closePrompt);
        Assert.DoesNotContain("if (offene.Count == 0) return true", closePrompt);
        Assert.DoesNotContain("if (decision == CodingOpenStretchDamageDialogDecision.Close)", closePrompt);
        Assert.DoesNotContain("if (decision == CodingOpenStretchDamageDialogDecision.Cancel)", closePrompt);
        Assert.DoesNotContain("CodingOpenStretchDamagePolicy.ResolveCloseMeter", closePrompt);
        Assert.DoesNotContain("_codingSessionService?.UpdateEvent", closePrompt);
        Assert.DoesNotContain(".ConfirmClose(openEvents, closeMeter))", closePrompt);
        Assert.DoesNotContain("DialogHost.Current", closePrompt);
        Assert.DoesNotContain("DialogConfirm", closePrompt);
        Assert.DoesNotContain("new System.Text.StringBuilder", closePrompt);
        Assert.DoesNotContain("Folgende Streckensch", closePrompt);
        Assert.DoesNotContain(".Where(e => e.Entry.IsStreckenschaden", closePrompt);
        Assert.DoesNotContain("ev.MeterAtCapture > start", closePrompt);
        Assert.Contains("public static string Build", policy);
        Assert.Contains("public static IReadOnlyList<CodingEvent> FindOpen", closePolicy);
        Assert.Contains("CodingOpenStretchDamagePolicy.ResolveCloseMeter", closeApplier);
        Assert.Contains("codingSessionService?.UpdateEvent", closeApplier);
        Assert.Contains("CodingOpenStretchDamagePromptBuilder.Build", dialogService);
        Assert.Contains("CodingOpenStretchDamageDialogDecision", dialogService);
        Assert.Contains("DialogHost.Current", dialogServiceFactory);
        Assert.Contains("ConfirmCancel", dialogServiceFactory);
        Assert.Contains("CodingOpenStretchDamageDialogServiceFactory.Create", dialogWorkflow);
        Assert.Contains("new CodingOpenStretchDamageDialogWorkflowActions", dialogWorkflow);
        Assert.Contains("actions.RunWithSuspendedOverlay", dialogWorkflow);
        Assert.Contains("service.ConfirmClose(openEvents, closeMeter)", dialogWorkflow);
        Assert.Contains("actions.FindOpen", commandWorkflow);
        Assert.Contains("actions.ConfirmClose", commandWorkflow);
        Assert.Contains("actions.ApplyClose", commandWorkflow);
        Assert.Contains("actions.RefreshEvents()", commandWorkflow);
    }
}
