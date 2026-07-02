using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingTrainingArchitectureTests
{
    [Fact]
    public void PlayerWindow_training_sample_persistence_lives_in_coordinator()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var persistencePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Persistence.cs");
        var codingStatePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.State.cs");
        var ownerPath = Path.Combine(uiRoot, "Player", "CodingTrainingSamplesOwner.cs");
        var coordinatorPath = Path.Combine(uiRoot, "Ai", "CodingTrainingSamplePersistenceCoordinator.cs");
        var batchWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingTrainingBatchPersistenceWorkflow.cs");

        Assert.True(File.Exists(ownerPath), "Training-Sample-Coordinator-Cache soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(coordinatorPath), "Training-Sample-Persistenz soll ausserhalb von PlayerWindow orchestriert werden.");
        Assert.True(File.Exists(batchWorkflowPath), "Training-Batch-Persistenz-Guard soll ausserhalb von PlayerWindow liegen.");

        var persistence = File.ReadAllText(persistencePath);
        var codingState = File.ReadAllText(codingStatePath);
        var owner = File.Exists(ownerPath) ? File.ReadAllText(ownerPath) : "";
        var coordinator = File.ReadAllText(coordinatorPath);
        var batchWorkflow = File.ReadAllText(batchWorkflowPath);

        Assert.Contains("CodingTrainingSamplePersistenceCoordinator", persistence);
        Assert.DoesNotContain("private CodingTrainingSamplePersistenceCoordinator? _codingTrainingSamples", persistence);
        Assert.DoesNotContain("CodingTrainingSamplePersistenceCoordinator.CreateDefault", persistence);
        Assert.Contains("private readonly CodingTrainingSamplesOwner _codingTrainingSamplesOwner", codingState);
        Assert.Contains("public sealed class CodingTrainingSamplesOwner", owner);
        Assert.Contains("CodingTrainingSamplePersistenceCoordinator.CreateDefault", owner);
        Assert.Contains("CodingTrainingBatchPersistenceWorkflow.Execute", persistence);
        Assert.Contains("_codingSessionHost", persistence);
        Assert.DoesNotContain("_codingVm", persistence);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel || events is null || events.Count == 0) return", persistence);
        Assert.DoesNotContain("events.Count == 0", persistence);
        Assert.DoesNotContain("CodingTrainingFrameStore", persistence);
        Assert.DoesNotContain("CodingTrainingSamplePersister", persistence);
        Assert.DoesNotContain("CodingTrainingSampleEvalProtector", persistence);
        Assert.DoesNotContain("CodingTrainingSampleFactory.Create", persistence);
        Assert.DoesNotContain("SaveGoldFrameAsync", persistence);
        Assert.DoesNotContain("SaveEvidenceFrame", persistence);
        Assert.DoesNotContain("IsCodingSampleEvalProtected", persistence);
        Assert.DoesNotContain("TrainingSampleEligibility", persistence);
        Assert.DoesNotContain("Environment.UserName", persistence);
        Assert.Contains("PlayerUserNameProvider.Current", persistence);
        Assert.Contains("SaveGoldFrameAsync", coordinator);
        Assert.Contains("CodingTrainingSampleFactory.Create", coordinator);
        Assert.Contains("CodingTrainingSampleEvalProtector", coordinator);
        Assert.Contains("TrainingSampleEligibility.TryParseInspectionDate", coordinator);
        Assert.Contains("request.Events is null || request.Events.Count == 0", batchWorkflow);
        Assert.Contains("actions.PersistEvents(request.Events)", batchWorkflow);
    }
}
