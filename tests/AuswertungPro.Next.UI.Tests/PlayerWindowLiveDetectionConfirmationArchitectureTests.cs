using System;
using System.Collections.Generic;
using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowLiveDetectionConfirmationArchitectureTests
{
    [Fact]
    public void PlayerWindow_live_detection_confirmation_actions_live_in_actions_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var confirmationPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Confirmation.cs");
        var actionsPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Confirmation.Actions.cs");
        var trainingPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Confirmation.Training.cs");
        var statusControlsPath = Path.Combine(windowsRoot, "LiveDetectionStatusControls.cs");
        var correctionSelectionPath = Path.Combine(uiRoot, "Ai", "LiveDetectionCorrectionCodeSelectionService.cs");
        var correctionSelectionFactoryPath = Path.Combine(uiRoot, "Ai", "LiveDetectionCorrectionCodeSelectionServiceFactory.cs");
        var correctionSelectionWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionCorrectionCodeSelectionWorkflow.cs");
        var displayWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionConfirmationDisplayWorkflow.cs");
        var frameExporterPath = Path.Combine(uiRoot, "Ai", "LiveDetectionTrainingFrameExporter.cs");
        var exportPlannerPath = Path.Combine(uiRoot, "Ai", "LiveDetectionTrainingExportPlanner.cs");
        var annotationWriterPath = Path.Combine(uiRoot, "Ai", "LiveDetectionTrainingAnnotationWriter.cs");
        var trainingWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionConfirmationTrainingWorkflow.cs");
        var trainingResultWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionConfirmationTrainingResultWorkflow.cs");
        var acceptCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionConfirmationAcceptCommandWorkflow.cs");
        var correctCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionConfirmationCorrectCommandWorkflow.cs");
        var skipCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionConfirmationSkipCommandWorkflow.cs");
        var trainingControllerPath = Path.Combine(uiRoot, "Player", "LiveDetectionConfirmationTrainingController.cs");
        var trainingControllerSetFactoryPath = Path.Combine(uiRoot, "Player", "LiveDetectionTrainingControllerSetFactory.cs");
        var playerWindowPath = Path.Combine(windowsRoot, "PlayerWindow.xaml.cs");

        Assert.True(File.Exists(actionsPath), "LiveDetection-Bestaetigungsaktionen sollen aus dem Anzeige-Partial heraus.");
        Assert.True(File.Exists(trainingPath), "LiveDetection-Trainingsuebernahme soll aus den simplen Bestaetigungsaktionen heraus.");
        Assert.True(File.Exists(correctionSelectionPath), "LiveDetection-Korrektur-Codeauswahl soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(correctionSelectionFactoryPath), "LiveDetection-Korrektur-Codeauswahl soll ueber Factory verdrahtet werden.");
        Assert.True(File.Exists(correctionSelectionWorkflowPath), "LiveDetection-Korrektur-Codeauswahl-Serviceaufruf soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(displayWorkflowPath), "LiveDetection-Bestaetigungsanzeige und Resume-Entscheidung sollen ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(frameExporterPath), "Detection-Training-Frame-Export soll ausserhalb der PlayerWindow-Partials gekapselt sein.");
        Assert.True(File.Exists(exportPlannerPath), "Detection-Training-Exportplanung soll ausserhalb der PlayerWindow-Partials gekapselt sein.");
        Assert.True(File.Exists(annotationWriterPath), "Detection-Training-Annotationen sollen ausserhalb der PlayerWindow-Partials geschrieben werden.");
        Assert.True(File.Exists(trainingWorkflowPath), "Detection-Confirmation-Training-Ablauf soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(trainingResultWorkflowPath), "Detection-Confirmation-Training-Ergebnisbehandlung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(acceptCommandWorkflowPath), "Detection-Accept-Befehlsreihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(correctCommandWorkflowPath), "Detection-Correct-Befehlsreihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(skipCommandWorkflowPath), "Detection-Skip-Befehlsreihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(trainingControllerPath), "Detection-Bestaetigungssteuerung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(trainingControllerSetFactoryPath), "Detection-Trainingscontroller sollen ausserhalb von PlayerWindow aufgebaut werden.");

        var confirmation = File.ReadAllText(confirmationPath);
        var actions = File.ReadAllText(actionsPath);
        var training = File.ReadAllText(trainingPath);
        var statusControls = File.ReadAllText(statusControlsPath);
        var correctionSelection = File.ReadAllText(correctionSelectionPath);
        var correctionSelectionFactory = File.ReadAllText(correctionSelectionFactoryPath);
        var correctionSelectionWorkflow = File.Exists(correctionSelectionWorkflowPath) ? File.ReadAllText(correctionSelectionWorkflowPath) : "";
        var displayWorkflow = File.Exists(displayWorkflowPath) ? File.ReadAllText(displayWorkflowPath) : "";
        var frameExporter = File.ReadAllText(frameExporterPath);
        var exportPlanner = File.ReadAllText(exportPlannerPath);
        var annotationWriter = File.ReadAllText(annotationWriterPath);
        var trainingWorkflow = File.Exists(trainingWorkflowPath) ? File.ReadAllText(trainingWorkflowPath) : "";
        var trainingResultWorkflow = File.Exists(trainingResultWorkflowPath) ? File.ReadAllText(trainingResultWorkflowPath) : "";
        var acceptCommandWorkflow = File.Exists(acceptCommandWorkflowPath) ? File.ReadAllText(acceptCommandWorkflowPath) : "";
        var correctCommandWorkflow = File.Exists(correctCommandWorkflowPath) ? File.ReadAllText(correctCommandWorkflowPath) : "";
        var skipCommandWorkflow = File.Exists(skipCommandWorkflowPath) ? File.ReadAllText(skipCommandWorkflowPath) : "";
        var trainingController = File.ReadAllText(trainingControllerPath);
        var trainingControllerSetFactory = File.ReadAllText(trainingControllerSetFactoryPath);
        var playerWindow = File.ReadAllText(playerWindowPath);

        Assert.Contains("private void ShowDetectionConfirmation", confirmation);
        Assert.Contains("private void ResumeDetection", confirmation);
        Assert.Contains("LiveDetectionConfirmationDisplayWorkflow.Show", confirmation);
        Assert.Contains("LiveDetectionConfirmationDisplayWorkflow.Resume", confirmation);
        AssertNoForbiddenTokens(
            confirmation,
            "PlayerConfirmationPlayback.PauseLiveDetectionConfirmation",
            "if (_detectionConfirmationBuffer.TimestampSeconds.HasValue)",
            "if (!_playerPlaybackControlHost.IsPlaying)",
            "TxtDetectionFinding.Text",
            "TxtDetectionDetail.Text",
            "DetectionConfirmationPanel.Visibility = Visibility.Visible",
            "DetectionConfirmationPanel.Visibility = Visibility.Collapsed",
            "private async void DetectionAccept_Click",
            "private async void DetectionCorrect_Click",
            "private void DetectionSkip_Click");
        Assert.Contains("LiveDetectionStatusControls.ShowDetectionConfirmation", confirmation);
        Assert.Contains("LiveDetectionStatusControls.HideDetectionConfirmation", confirmation);
        AssertNoForbiddenTokens(
            actions,
            "private async void DetectionAccept_Click",
            "private async void DetectionCorrect_Click",
            "ResumeDetection();",
            "TrainingAnnotationExportServiceFactory.Create");
        Assert.Contains("private void DetectionSkip_Click", actions);
        Assert.Contains("LiveDetectionConfirmationSkipCommandWorkflow.Execute", actions);
        AssertNoForbiddenTokens(
            training,
            "private async void DetectionAccept_Click",
            "private async void DetectionCorrect_Click",
            "if (pendingFindings.Count == 0)",
            "\n        try",
            "catch (Exception ex)",
            "selectedEntry == null",
            "LiveDetectionCorrectionCodeSelectionServiceFactory.Create",
            "CodingExplorerEntryFactory.CreateSeed",
            "VsaCodeExplorerDialogServiceFactory.Create",
            "if (!result.Saved)",
            "result.SavedCount",
            "result.Code",
            "foreach (var finding in _detectionPendingFindings)",
            "annotationWriter.SaveAcceptedAsync",
            "annotationWriter.SaveCorrectedAsync",
            "TrainingAnnotationExportServiceFactory.Create",
            "LiveDetectionTrainingFrameExporter",
            "LiveDetectionTrainingExportPlanner.BuildAccepted",
            "LiveDetectionTrainingExportPlanner.BuildCorrected",
            "LiveDetectionTrainingAnnotationWriter.CreateDefault",
            "LiveDetectionConfirmationAcceptCommandWorkflow.ExecuteAsync",
            "LiveDetectionConfirmationCorrectCommandWorkflow.ExecuteAsync",
            "LiveDetectionCorrectionCodeSelectionWorkflow.Select",
            "LiveDetectionConfirmationTrainingWorkflow.SaveAcceptedAsync",
            "LiveDetectionConfirmationTrainingWorkflow.SaveCorrectedAsync",
            "LiveDetectionConfirmationTrainingResultWorkflow.ExecuteAccepted",
            "LiveDetectionConfirmationTrainingResultWorkflow.ExecuteCorrected",
            "HandleDetectionAcceptAsync",
            "HandleDetectionCorrectAsync",
            "VsaYoloClassMap.GetClassId",
            "BBoxFromClockPosition",
            "det_corr_",
            "File.WriteAllBytesAsync",
            "File.Delete",
            "Path.GetTempPath",
            "TeacherAnnotationStore.AppendAsync");
        Assert.Contains("private void DetectionAccept_Click", training);
        Assert.Contains("private void DetectionCorrect_Click", training);
        Assert.Contains(".SafeFireAndForget(\"DetectionAccept\")", training);
        Assert.Contains(".SafeFireAndForget(\"DetectionCorrect\")", training);
        Assert.Contains("_liveDetectionConfirmationTrainingController.AcceptAsync()", training);
        Assert.Contains("_liveDetectionConfirmationTrainingController.CorrectAsync()", training);
        Assert.Contains("public sealed class LiveDetectionConfirmationTrainingController", trainingController);
        Assert.Contains("ILiveDetectionTrainingAnnotationWriter", trainingController);
        Assert.Contains("LiveDetectionConfirmationAcceptCommandWorkflow.ExecuteAsync", trainingController);
        Assert.Contains("LiveDetectionConfirmationCorrectCommandWorkflow.ExecuteAsync", trainingController);
        Assert.Contains("LiveDetectionConfirmationTrainingWorkflow.SaveAcceptedAsync", trainingController);
        Assert.Contains("LiveDetectionConfirmationTrainingWorkflow.SaveCorrectedAsync", trainingController);
        Assert.Contains("LiveDetectionConfirmationTrainingResultWorkflow.ExecuteAccepted", trainingController);
        Assert.Contains("LiveDetectionConfirmationTrainingResultWorkflow.ExecuteCorrected", trainingController);
        Assert.DoesNotContain("LiveDetectionTrainingAnnotationWriter.CreateDefault", trainingController);
        Assert.Contains("LiveDetectionTrainingControllerSetFactory.Create", playerWindow);
        Assert.DoesNotContain("new LiveDetectionConfirmationTrainingController", playerWindow);
        Assert.DoesNotContain("LiveDetectionTrainingAnnotationWriter.CreateDefault", playerWindow);
        Assert.Contains("new LiveDetectionConfirmationTrainingController", trainingControllerSetFactory);
        Assert.Equal(1, CountOccurrences(trainingControllerSetFactory, "LiveDetectionTrainingAnnotationWriter.CreateDefault()"));
        Assert.Contains("var trainingResult = await actions.SaveAcceptedAsync()", acceptCommandWorkflow);
        Assert.Contains("actions.HandleAcceptedResult(trainingResult)", acceptCommandWorkflow);
        Assert.Contains("actions.ShowOsdMeterStatus($\"\\u2717 Fehler: {ex.Message}\", false)", acceptCommandWorkflow);
        Assert.Contains("actions.ResumeDetection()", acceptCommandWorkflow);
        Assert.Contains("var selectedEntry = actions.SelectCorrection()", correctCommandWorkflow);
        Assert.Contains("var trainingResult = await actions.SaveCorrectedAsync(selectedEntry)", correctCommandWorkflow);
        Assert.Contains("actions.HandleCorrectedResult(trainingResult)", correctCommandWorkflow);
        Assert.Contains("actions.ShowOsdMeterStatus($\"\\u2717 Fehler: {ex.Message}\", false)", correctCommandWorkflow);
        Assert.Contains("actions.ResumeDetection()", correctCommandWorkflow);
        Assert.Contains("actions.ResumeDetection()", skipCommandWorkflow);
        Assert.Contains("public static void ShowDetectionConfirmation", statusControls);
        Assert.Contains("public static void HideDetectionConfirmation", statusControls);
        Assert.Contains("PlayerConfirmationPlayback.PauseLiveDetectionConfirmation", displayWorkflow);
        Assert.Contains("SeekMilliseconds", displayWorkflow);
        Assert.Contains("CodingExplorerEntryFactory.CreateSeed", correctionSelection);
        Assert.Contains("VsaCodeExplorerDialogServiceFactory.Create", correctionSelectionFactory);
        Assert.Contains("LiveDetectionCorrectionCodeSelectionServiceFactory.Create", correctionSelectionWorkflow);
        Assert.Contains("service.Select(", correctionSelectionWorkflow);
        Assert.Contains("public sealed class LiveDetectionTrainingFrameExporter", frameExporter);
        Assert.Contains("File.WriteAllBytesAsync", frameExporter);
        Assert.Contains("BestEffort.Try", frameExporter);
        Assert.Contains("public static class LiveDetectionTrainingExportPlanner", exportPlanner);
        Assert.Contains("VsaYoloClassMap.GetClassId", exportPlanner);
        Assert.Contains("LiveDetectionGeometryMapper.BBoxFromClockPosition", exportPlanner);
        Assert.Contains("public interface ILiveDetectionTrainingAnnotationWriter", annotationWriter);
        Assert.Contains("public sealed class LiveDetectionTrainingAnnotationWriter : ILiveDetectionTrainingAnnotationWriter", annotationWriter);
        Assert.Contains("TrainingAnnotationExportServiceFactory.Create", annotationWriter);
        Assert.Contains("LiveDetectionTrainingExportPlanner.BuildAccepted", annotationWriter);
        Assert.Contains("LiveDetectionTrainingExportPlanner.BuildCorrected", annotationWriter);
        Assert.Contains("TeacherAnnotationStore.AppendAsync", annotationWriter);
        Assert.Contains("saveAcceptedAsync", trainingWorkflow);
        Assert.Contains("saveCorrectedAsync", trainingWorkflow);
        Assert.Contains("if (!trainingResult.Saved)", trainingResultWorkflow);
        Assert.Contains("actions.ShowOsdMeterStatus($\"\\u2713 {trainingResult.SavedCount} Befund(e) gespeichert\", true)", trainingResultWorkflow);
        Assert.Contains("actions.ShowOsdMeterStatus($\"\\u2713 Training: {trainingResult.Code} (korrigiert)\", true)", trainingResultWorkflow);
        Assert.Contains("actions.ResumeDetection()", trainingResultWorkflow);
    }

    private static void AssertNoForbiddenTokens(string source, params string[] forbiddenTokens)
    {
        var hits = new List<string>();
        foreach (var token in forbiddenTokens)
        {
            if (source.Contains(token, StringComparison.Ordinal))
                hits.Add(token);
        }

        Assert.True(
            hits.Count == 0,
            "Verbotene alte LiveDetection-Confirmation-Logik gefunden: " + string.Join(", ", hits));
    }

    private static int CountOccurrences(string source, string value)
        => (source.Length - source.Replace(value, "", StringComparison.Ordinal).Length) / value.Length;
}
