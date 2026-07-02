using System;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class UiArchitectureGuardTests
{
    [Fact]
    public void PlayerWindow_current_code_badge_uses_controls_adapter()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var navigationPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Navigation.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingCurrentCodeUpdateWorkflow.cs");
        var meterResolveWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingDisplayMeterResolveWorkflow.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingCurrentCodeBadgeControls.cs");

        Assert.True(File.Exists(workflowPath), "Current-Code-Badge-Entscheidung soll ausserhalb der PlayerWindow-Partials laufen.");
        Assert.True(File.Exists(meterResolveWorkflowPath), "Current-Code-Display-Meter-Gate soll ausserhalb der PlayerWindow-Partials laufen.");
        Assert.True(File.Exists(controlsPath), "Current-Code-Badge-Text und Visibility sollen ausserhalb der PlayerWindow-Partials gesetzt werden.");

        var navigation = File.ReadAllText(navigationPath);
        var workflow = File.ReadAllText(workflowPath);
        var meterResolveWorkflow = File.Exists(meterResolveWorkflowPath) ? File.ReadAllText(meterResolveWorkflowPath) : "";
        var controls = File.ReadAllText(controlsPath);

        Assert.Contains("CodingCurrentCodeUpdateWorkflow.Execute", navigation);
        Assert.Contains("CodingDisplayMeterResolveWorkflow.Execute", navigation);
        Assert.Contains("CodingCurrentCodeBadgeControls.Apply", navigation);
        Assert.DoesNotContain("CodingCurrentCodeBadgePolicy.Build", navigation);
        Assert.DoesNotContain("=> !_codingSessionHost.HasViewModel", navigation);
        Assert.Contains("if (!request.HasCodingViewModel)", meterResolveWorkflow);
        Assert.Contains("actions.ResolveDisplayMeter()", meterResolveWorkflow);
        Assert.Contains("CodingCurrentCodeBadgePolicy.Build", workflow);
        Assert.Contains("CodingCurrentCodeBadgeState.Hidden", workflow);
        Assert.DoesNotContain("TxtCodingCurrentCode.Text", navigation);
        Assert.DoesNotContain("CodingCurrentCodeBadge.Visibility", navigation);
        Assert.Contains("public static class CodingCurrentCodeBadgeControls", controls);
        Assert.Contains("TextBlock", controls);
        Assert.Contains("Visibility.Visible", controls);
        Assert.Contains("Visibility.Collapsed", controls);
    }

    [Fact]
    public void PlayerWindow_meter_timeline_uses_controls_adapter()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var navigationPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Navigation.cs");
        var sessionPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.Session.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingMeterTimelineControls.cs");

        Assert.True(File.Exists(controlsPath), "Meteranzeige und Timeline-Playhead sollen ausserhalb der PlayerWindow-Partials gesetzt werden.");

        var navigation = File.ReadAllText(navigationPath);
        var session = File.ReadAllText(sessionPath);
        var controls = File.ReadAllText(controlsPath);
        var playerText = navigation + session;

        Assert.Contains("CodingMeterTimelineControls.Apply", navigation);
        Assert.Contains("CodingMeterTimelineControls.SetText", session);
        Assert.DoesNotContain("TxtCodingMeter.Text", playerText);
        Assert.DoesNotContain("PipeTimeline.CurrentMeter", playerText);
        Assert.Contains("public static class CodingMeterTimelineControls", controls);
        Assert.Contains("PipeGraphTimeline", controls);
        Assert.Contains("meterText.Text", controls);
        Assert.Contains("timeline.CurrentMeter", controls);
    }

    [Fact]
    public void PlayerWindow_coding_mode_dialogs_live_in_service()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var lifecyclePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.cs");
        var sessionPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.Session.cs");
        var trainingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var servicePath = Path.Combine(uiRoot, "Ai", "CodingModeDialogService.cs");
        var factoryPath = Path.Combine(uiRoot, "Ai", "CodingModeDialogServiceFactory.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingModeDialogWorkflow.cs");

        Assert.True(File.Exists(servicePath), "Coding-Modus-Dialogtexte muessen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(factoryPath), "Coding-Modus-DialogHost-Verdrahtung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "Coding-Modus-Dialogaufrufe sollen ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var lifecycle = File.ReadAllText(lifecyclePath);
        var session = File.ReadAllText(sessionPath);
        var training = File.ReadAllText(trainingPath);
        var playerText = lifecycle + session + training;
        var service = File.ReadAllText(servicePath);
        var factory = File.ReadAllText(factoryPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        Assert.DoesNotContain("CodingModeDialogServiceFactory.Create", playerText);
        Assert.DoesNotContain("new CodingModeDialogWorkflowActions", playerText);
        Assert.Contains("CodingModeDialogWorkflow.ShowMissingHaltung", lifecycle);
        Assert.Contains("CodingModeDialogWorkflow.ShowSessionStartFailed", session);
        Assert.DoesNotContain(".ShowMissingHaltung()", playerText);
        Assert.DoesNotContain(".ShowSessionStartFailed(message)", playerText);
        Assert.DoesNotContain("DialogHost.Current", playerText);
        Assert.DoesNotContain("Codier-Modus ben", playerText);
        Assert.DoesNotContain("Frame konnte nicht aufgenommen werden.", playerText);
        Assert.Contains("ShowMissingHaltung", service);
        Assert.Contains("ShowSessionStartFailed", service);
        Assert.Contains("ShowImportFrameCaptureFailed", service);
        Assert.Contains("CodingModeDialogServiceFactory.Create", workflow);
        Assert.Contains("new CodingModeDialogWorkflowActions", workflow);
        Assert.Contains("service.ShowMissingHaltung()", workflow);
        Assert.Contains("service.ShowSessionStartFailed(message)", workflow);
        Assert.Contains("DialogHost.Current", factory);
    }

    [Fact]
    public void PlayerWindow_ai_event_partials_read_session_state_through_session_host()
    {
        var root = FindRepositoryRoot();
        var windowsRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Windows");
        var paths = new[]
        {
            Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.Live.cs"),
            Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.MultiModel.cs"),
            Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Classifier.Boundary.cs"),
            Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Classifier.Structural.cs"),
            Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Streckenschaden.cs")
        };

        foreach (var path in paths)
        {
            Assert.True(File.Exists(path), $"{Path.GetFileName(path)} muss als PlayerWindow-Partial existieren.");
            var text = File.ReadAllText(path);
            Assert.Contains("_codingSessionHost", text);
            Assert.DoesNotContain("_codingVm", text);
        }
    }

}
