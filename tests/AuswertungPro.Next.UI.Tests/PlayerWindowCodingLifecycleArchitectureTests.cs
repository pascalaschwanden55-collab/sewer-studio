using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingLifecycleArchitectureTests
{
    [Fact]
    public void PlayerWindow_terminal_exit_boundary_check_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var codingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Exit.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingModeExitFinalizationWorkflow.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingTerminalBoundaryPresencePolicy.cs");

        Assert.True(File.Exists(codingPath), "Coding-Exit-Cleanup soll in einem eigenen Partial liegen.");
        Assert.True(File.Exists(workflowPath), "Coding-Exit-Finalisierung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(policyPath), "Exit-Pruefung fuer BCE/BDC* muss ausserhalb der PlayerWindow-Partials liegen.");

        var coding = File.ReadAllText(codingPath);
        var workflow = File.ReadAllText(workflowPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingModeExitFinalizationWorkflow.Execute", coding);
        Assert.Contains("_codingSessionHost.EventCollection", coding);
        Assert.Contains("_codingSessionHost.EndMeter", coding);
        Assert.Contains("HasCodingViewModel: _codingSessionHost.HasViewModel", coding);
        Assert.DoesNotContain("_codingVm?.Events", coding);
        Assert.DoesNotContain("_codingVm?.EndMeter", coding);
        Assert.DoesNotContain("HasCodingViewModel: _codingVm is not null", coding);
        Assert.DoesNotContain("CodingTerminalBoundaryPresencePolicy.HasEndOrAbortCode", coding);
        Assert.Contains("CodingTerminalBoundaryPresencePolicy.HasEndOrAbortCode", workflow);
        Assert.DoesNotContain("string.Equals(e.Entry.Code, \"BCE\"", coding + workflow);
        Assert.DoesNotContain("string.Equals(e.Entry.Code, \"BDC\"", coding + workflow);
        Assert.Contains("public static bool HasEndOrAbortCode", policy);
        Assert.Contains("MainCode(e.Entry.Code) is \"BCE\" or \"BDC\"", policy);
    }

    [Fact]
    public void PlayerWindow_dn_calibration_initialization_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var codingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Session.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingDnCalibrationPolicy.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingDnCalibrationApplyWorkflow.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingSessionHeaderControls.cs");

        Assert.True(File.Exists(policyPath), "DN-/Kalibrierungsinitialisierung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "DN-/Kalibrierungs-Anwendungsreihenfolge muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controlsPath), "DN-/Range-Anzeigetexte sollen ausserhalb der PlayerWindow-Partials gesetzt werden.");

        var coding = File.ReadAllText(codingPath);
        var policy = File.ReadAllText(policyPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var controls = File.ReadAllText(controlsPath);

        Assert.Contains("CodingDnCalibrationPolicy.Build", coding);
        Assert.Contains("CodingDnCalibrationApplyWorkflow.Execute", coding);
        Assert.Contains("CodingSessionHeaderControls.ApplyCalibration", coding);
        Assert.Contains("CodingSessionHeaderControls.SetRangeText", coding);
        Assert.DoesNotContain("if (_haltungRecord == null || !_codingOverlayRuntimeOwner.HasService)", coding);
        Assert.DoesNotContain("var dnCalibration = CodingDnCalibrationPolicy.Build", coding);
        Assert.DoesNotContain("if (dnCalibration.Calibration != null)", coding);
        Assert.DoesNotContain("_haltungRecord.Fields.TryGetValue(\"DN_mm\"", coding);
        Assert.DoesNotContain("int.TryParse(dnStr", coding);
        Assert.DoesNotContain("TxtCodingCalibDn.Text", coding);
        Assert.DoesNotContain("TxtCodingCalibStatus.Text", coding);
        Assert.DoesNotContain("TxtCodingRange.Text", coding);
        Assert.Contains("if (!request.HasHaltungRecord || !request.HasOverlayService)", workflow);
        Assert.Contains("actions.BuildCalibration()", workflow);
        Assert.Contains("actions.SetCalibration(dnCalibration.Calibration)", workflow);
        Assert.Contains("actions.ApplyCalibrationControls(dnCalibration)", workflow);
        Assert.Contains("public static CodingDnCalibrationState Build", policy);
        Assert.Contains("new PipeCalibration", policy);
        Assert.Contains("public static class CodingSessionHeaderControls", controls);
        Assert.Contains("ApplyCalibration", controls);
        Assert.Contains("SetRangeText", controls);
    }

    [Fact]
    public void PlayerWindow_haltungslaenge_fallback_lives_in_lifecycle_length_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var lifecyclePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.cs");
        var persistencePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Persistence.cs");
        var lengthPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Length.cs");
        var ensureServicePath = Path.Combine(uiRoot, "Ai", "CodingHaltungslaengeEnsureService.cs");
        var ensureServiceFactoryPath = Path.Combine(uiRoot, "Ai", "CodingHaltungslaengeEnsureServiceFactory.cs");
        var ensureWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingHaltungslaengeEnsureWorkflow.cs");
        var enterWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingModeEnterWorkflow.cs");

        Assert.True(File.Exists(lengthPath), "Haltungslaenge-Fallback gehoert in eine Lifecycle-Length-Partial, nicht in Persistence.");
        Assert.True(File.Exists(ensureServicePath), "Haltungslaenge-Fallbacklogik gehoert ausserhalb der PlayerWindow-Partials.");
        Assert.True(File.Exists(ensureServiceFactoryPath), "Haltungslaenge-Eingabe soll ueber Factory verdrahtet werden.");
        Assert.True(File.Exists(ensureWorkflowPath), "Haltungslaenge-Fallbackaufruf soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(enterWorkflowPath), "Coding-Mode-Enter-Reihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var lifecycle = File.ReadAllText(lifecyclePath);
        var persistence = File.ReadAllText(persistencePath);
        var length = File.ReadAllText(lengthPath);
        var ensureService = File.ReadAllText(ensureServicePath);
        var ensureServiceFactory = File.ReadAllText(ensureServiceFactoryPath);
        var ensureWorkflow = File.Exists(ensureWorkflowPath) ? File.ReadAllText(ensureWorkflowPath) : "";
        var enterWorkflow = File.ReadAllText(enterWorkflowPath);

        Assert.Contains("EnsureHaltungslaenge: () => EnsureHaltungslaenge(_protocolContext.HaltungRecord!)", lifecycle);
        Assert.Contains("actions.EnsureHaltungslaenge()", enterWorkflow);
        Assert.DoesNotContain("private void EnsureHaltungslaenge", persistence);
        Assert.DoesNotContain("Microsoft.VisualBasic.Interaction.InputBox", persistence);
        Assert.Contains("private void EnsureHaltungslaenge", length);
        Assert.DoesNotContain("CodingHaltungslaengeEnsureServiceFactory.Create", length);
        Assert.DoesNotContain("new CodingHaltungslaengeEnsureWorkflowActions", length);
        Assert.Contains("CodingHaltungslaengeEnsureWorkflow.Ensure", length);
        Assert.DoesNotContain(".Ensure(record, _damageOverlay?.PipeLengthMeters)", length);
        Assert.DoesNotContain("CodingHaltungslaengeResolver.TryEnsureFromKnownSources", length);
        Assert.DoesNotContain("Microsoft.VisualBasic.Interaction.InputBox", length);
        Assert.DoesNotContain("SetFieldValue(\"Haltungslaenge_m\"", length);
        Assert.Contains("CodingHaltungslaengeResolver.TryEnsureFromKnownSources", ensureServiceFactory);
        Assert.Contains("Microsoft.VisualBasic.Interaction.InputBox", ensureServiceFactory);
        Assert.Contains("CodingHaltungslaengeEnsureServiceFactory.Create", ensureWorkflow);
        Assert.Contains("new CodingHaltungslaengeEnsureWorkflowActions", ensureWorkflow);
        Assert.Contains("service.Ensure(record, overlayPipeLengthMeters)", ensureWorkflow);
        Assert.Contains("SetFieldValue", ensureService);
        Assert.Contains("\"Haltungslaenge_m\"", ensureService);
    }
}
