using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingTrainingArchitectureTests
{
    [Fact]
    public void PlayerWindow_training_sample_persistence_lives_in_coordinator()
    {
        var persistencePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Persistence.cs");
        var codingStatePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.State.cs");
        var ownerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingTrainingSamplesOwner.cs");
        var coordinatorPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingTrainingSamplePersistenceCoordinator.cs");
        var batchWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingTrainingBatchPersistenceWorkflow.cs");

        Assert.True(File.Exists(ownerPath), "Training-Sample-Coordinator-Cache soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(coordinatorPath), "Training-Sample-Persistenz soll ausserhalb von PlayerWindow orchestriert werden.");
        Assert.True(File.Exists(batchWorkflowPath), "Training-Batch-Persistenz-Guard soll ausserhalb von PlayerWindow liegen.");

        var persistence = File.ReadAllText(persistencePath);
        var codingState = File.ReadAllText(codingStatePath);
        var owner = File.Exists(ownerPath) ? File.ReadAllText(ownerPath) : "";
        var coordinator = File.ReadAllText(coordinatorPath);
        var batchWorkflow = File.ReadAllText(batchWorkflowPath);

        Assert.Contains("CodingTrainingSamplePersistenceCoordinator", persistence);
        Assert.Contains("private readonly CodingTrainingSamplesOwner _codingTrainingSamplesOwner", codingState);
        Assert.Contains("public sealed class CodingTrainingSamplesOwner", owner);
        Assert.Contains("CodingTrainingSamplePersistenceCoordinator.CreateDefault", owner);
        Assert.Contains("CodingTrainingBatchPersistenceWorkflow.Execute", persistence);
        Assert.Contains("_codingSessionHost", persistence);
        Assert.Contains("PlayerUserNameProvider.Current", persistence);
        Assert.Contains("SaveGoldFrameAsync", coordinator);
        Assert.Contains("CodingTrainingSampleFactory.Create", coordinator);
        Assert.Contains("CodingTrainingSampleEvalProtector", coordinator);
        Assert.Contains("TrainingSampleEligibility.TryParseInspectionDate", coordinator);
        Assert.Contains("request.Events is null || request.Events.Count == 0", batchWorkflow);
        Assert.Contains("actions.PersistEvents(request.Events)", batchWorkflow);
    }
}
