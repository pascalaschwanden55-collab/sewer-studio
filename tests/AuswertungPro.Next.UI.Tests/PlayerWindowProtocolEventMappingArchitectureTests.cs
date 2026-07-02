using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowProtocolEventMappingArchitectureTests
{
    [Fact]
    public void PlayerWindow_green_protocol_training_candidates_use_resolver()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var trainingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var resolverPath = Path.Combine(uiRoot, "Ai", "CodingProtocolTrainingCandidateResolver.cs");
        var runnerPath = Path.Combine(uiRoot, "Ai", "CodingProtocolGreenMatchTrainingRunner.cs");
        var confirmWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingProtocolImportTrainingConfirmationWorkflow.cs");
        var snapshotStorePath = Path.Combine(uiRoot, "Ai", "CodingProtocolTrainingSnapshotStore.cs");
        var workflowFactoryPath = Path.Combine(uiRoot, "Ai", "CodingProtocolImportTrainingWorkflowServiceFactory.cs");

        Assert.True(File.Exists(resolverPath), "Gruene Protokoll-Trainingskandidaten muessen ausserhalb der PlayerWindow-Partials auf Import-Events gemappt werden.");
        Assert.True(File.Exists(runnerPath), "Gruene Protokoll-Trainingskandidaten muessen ausserhalb der PlayerWindow-Partials abgearbeitet werden.");
        Assert.True(File.Exists(snapshotStorePath), "Gruene Protokoll-Trainingssnapshots sollen ausserhalb der PlayerWindow-Partials kopiert werden.");
        Assert.True(File.Exists(workflowFactoryPath), "Gruene Protokoll-Trainingsuebernahme soll ausserhalb der PlayerWindow-Partials verdrahtet werden.");

        var training = File.ReadAllText(trainingPath);
        var resolver = File.ReadAllText(resolverPath);
        var runner = File.Exists(runnerPath) ? File.ReadAllText(runnerPath) : "";
        var confirmWorkflow = File.Exists(confirmWorkflowPath) ? File.ReadAllText(confirmWorkflowPath) : "";
        var snapshotStore = File.ReadAllText(snapshotStorePath);
        var workflowFactory = File.ReadAllText(workflowFactoryPath);

        Assert.Contains("CodingProtocolGreenMatchTrainingRunner.AcceptGreenMatchesAsync", training);
        Assert.DoesNotContain("CodingProtocolTrainingCandidateResolver.ResolveImportEvents", training);
        Assert.Contains("CodingProtocolTrainingCandidateResolver.ResolveImportEvents", runner);
        Assert.Contains("public static async Task<CodingProtocolMatchOverlayState?> AcceptGreenMatchesAsync", runner);
        Assert.DoesNotContain("CodingProtocolImportTrainingWorkflowServiceFactory.Create", training);
        Assert.Contains("CodingProtocolImportTrainingWorkflowServiceFactory.Create", confirmWorkflow);
        Assert.DoesNotContain("CodingProtocolTrainingSnapshotStoreFactory.Create", training);
        Assert.DoesNotContain("Guid.TryParse(pair.Gt.RefId", training);
        Assert.DoesNotContain("_codingImportEvents.FirstOrDefault(ev => ev.Entry.EntryId", training);
        Assert.DoesNotContain("File.Exists", training);
        Assert.DoesNotContain("File.Copy", training);
        Assert.DoesNotContain("File.Delete", training);
        Assert.Contains("public static IReadOnlyList<CodingEvent> ResolveImportEvents", resolver);
        Assert.Contains("CodingProtocolTrainingSnapshotStoreFactory.Create", workflowFactory);
        Assert.Contains("File.Copy", snapshotStore);
        Assert.Contains("BestEffort.Try", snapshotStore);
    }

    [Fact]
    public void PlayerWindow_existing_protocol_entries_use_mapper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var protocolPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Protocol.cs");
        var mapperPath = Path.Combine(uiRoot, "Ai", "CodingProtocolEventMapper.cs");
        var appenderPath = Path.Combine(uiRoot, "Ai", "CodingProtocolEventCollectionAppender.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingExistingProtocolEntriesWorkflow.cs");

        Assert.True(File.Exists(mapperPath), "ProtocolEntry-zu-CodingEvent-Mapping muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(appenderPath), "Eintragen gemappter Protokoll-Events muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "Laden existierender Protokoll-Events soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var protocol = File.ReadAllText(protocolPath);
        var mapper = File.ReadAllText(mapperPath);
        var appender = File.Exists(appenderPath) ? File.ReadAllText(appenderPath) : "";
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        Assert.Contains("CodingExistingProtocolEntriesWorkflow.Execute", protocol);
        Assert.DoesNotContain("CodingProtocolEventMapper.BuildExistingEvents", protocol);
        Assert.DoesNotContain("CodingProtocolEventCollectionAppender.Append", protocol);
        Assert.Contains("_codingSessionHost", protocol);
        Assert.DoesNotContain("_codingVm", protocol);
        Assert.DoesNotContain("_codingVm.Events.Add", protocol);
        Assert.DoesNotContain("new CodingEvent", protocol);
        Assert.DoesNotContain("OrderBy(e => e.MeterStart ?? 0)", protocol);
        Assert.Contains("CodingProtocolEventMapper.BuildExistingEvents", workflow);
        Assert.Contains("CodingProtocolEventCollectionAppender.Append", workflow);
        Assert.Contains("public static IReadOnlyList<CodingEvent> BuildExistingEvents", mapper);
        Assert.Contains("target.Add", appender);
    }

    [Fact]
    public void PlayerWindow_import_protocol_events_use_mapper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var lifecyclePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.cs");
        var importPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Import.cs");
        var mapperPath = Path.Combine(uiRoot, "Ai", "CodingProtocolEventMapper.cs");
        var importWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingExistingProtocolImportEventsWorkflow.cs");
        var enterWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingModeEnterWorkflow.cs");

        Assert.True(File.Exists(importPath), "Import-Referenz-Laden soll in einem eigenen Lifecycle-Partial liegen.");
        Assert.True(File.Exists(importWorkflowPath), "Import-Referenz-Mapping und Count-Update sollen ausserhalb von PlayerWindow orchestriert werden.");
        Assert.True(File.Exists(enterWorkflowPath), "Coding-Mode-Enter-Reihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var lifecycle = File.ReadAllText(lifecyclePath);
        var import = File.ReadAllText(importPath);
        var mapper = File.ReadAllText(mapperPath);
        var importWorkflow = File.ReadAllText(importWorkflowPath);
        var enterWorkflow = File.ReadAllText(enterWorkflowPath);

        Assert.Contains("LoadExistingProtocolEventsAsImport: LoadExistingProtocolEventsAsImport", lifecycle);
        Assert.Contains("actions.LoadExistingProtocolEventsAsImport()", enterWorkflow);
        Assert.DoesNotContain("CodingProtocolEventMapper.BuildMissingImportEvents", lifecycle);
        Assert.Contains("CodingExistingProtocolImportEventsWorkflow.Execute", import);
        Assert.DoesNotContain("CodingProtocolEventMapper.BuildMissingImportEvents", import);
        Assert.DoesNotContain("CodingProtocolEventCollectionAppender.Append", import);
        Assert.DoesNotContain("_codingImportEvents.Add", import);
        Assert.DoesNotContain("new CodingEvent", import);
        Assert.DoesNotContain("!e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code)", import);
        Assert.Contains("public static IReadOnlyList<CodingEvent> BuildMissingImportEvents", mapper);
        Assert.Contains("CodingProtocolEventMapper.BuildMissingImportEvents", importWorkflow);
        Assert.Contains("CodingProtocolEventCollectionAppender.Append", importWorkflow);
        Assert.Contains("actions.SetImportCount(totalCount)", importWorkflow);
    }
}
