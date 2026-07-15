using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingTrainingArchitectureTests
{
    [Fact]
    public void PlayerWindow_training_sample_persistence_lives_in_coordinator()
    {
        var persistencePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Persistence.cs");
        var contextPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingTrainingPersistenceContext.cs");
        var codingStatePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.State.cs");
        var playerRootPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs");
        var ownerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingTrainingSamplesOwner.cs");
        var coordinatorPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingTrainingSamplePersistenceCoordinator.cs");
        var batchWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingTrainingBatchPersistenceWorkflow.cs");

        Assert.False(File.Exists(persistencePath), "Training-Speicheradapter sollen kein PlayerWindow-Partial mehr sein.");
        Assert.True(File.Exists(contextPath), "Training-Speicheradapter sollen ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(ownerPath), "Training-Sample-Coordinator-Cache soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(coordinatorPath), "Training-Sample-Persistenz soll ausserhalb von PlayerWindow orchestriert werden.");
        Assert.True(File.Exists(batchWorkflowPath), "Training-Batch-Persistenz-Guard soll ausserhalb von PlayerWindow liegen.");

        var context = File.ReadAllText(contextPath);
        var codingState = File.ReadAllText(codingStatePath);
        var playerRoot = File.ReadAllText(playerRootPath);
        var playerWindowPartials = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(Path.GetDirectoryName(playerRootPath)!, "PlayerWindow*.cs")
                .Select(File.ReadAllText));
        var owner = File.Exists(ownerPath) ? File.ReadAllText(ownerPath) : "";
        var coordinator = File.ReadAllText(coordinatorPath);
        var batchWorkflow = File.ReadAllText(batchWorkflowPath);

        Assert.Contains("CodingTrainingSamplesOwner.CreateDefault", context);
        Assert.Contains("owner.Coordinator.PersistSingleEventAsync", context);
        Assert.Contains("private readonly Ai.CodingTrainingPersistenceContext _codingTrainingPersistenceContext", codingState);
        Assert.Contains("_codingTrainingPersistenceContext = CodingTrainingPersistenceContext.CreateDefault", playerRoot);
        Assert.Contains("public sealed class CodingTrainingSamplesOwner", owner);
        Assert.Contains("CodingTrainingSamplePersistenceCoordinator.CreateDefault", owner);
        Assert.Contains("CodingTrainingBatchPersistenceWorkflow.Execute", context);
        Assert.Contains("_codingSessionHost", playerRoot);
        Assert.Contains("_liveDetectionController.PendingConfirmationFrameBytes", playerRoot);
        Assert.Contains("PlayerUserNameProvider.Current", context);
        Assert.DoesNotContain("PersistSingleEventAsTrainingSample", playerWindowPartials);
        Assert.DoesNotContain("private void PersistCodingEventsAsTrainingSamples", playerWindowPartials);
        Assert.Contains("SaveGoldFrameAsync", coordinator);
        Assert.Contains("CodingTrainingSampleFactory.Create", coordinator);
        Assert.Contains("CodingTrainingSampleEvalProtector", coordinator);
        Assert.Contains("TrainingSampleEligibility.TryParseInspectionDate", coordinator);
        Assert.Contains("request.Events is null || request.Events.Count == 0", batchWorkflow);
        Assert.Contains("actions.PersistEvents(request.Events)", batchWorkflow);
    }
}
