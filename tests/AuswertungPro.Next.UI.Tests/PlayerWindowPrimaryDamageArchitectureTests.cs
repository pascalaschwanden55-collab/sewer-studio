using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowPrimaryDamageArchitectureTests
{
    [Fact]
    public void PlayerWindow_coding_primary_damage_text_uses_existing_mapper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var protocolPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Protocol.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingPrimaryDamageTextBuilder.cs");
        var synchronizerPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingPrimaryDamageSynchronizer.cs");
        var synchronizerFactoryPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingPrimaryDamageSynchronizerFactory.cs");
        var syncWorkflowPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingPrimaryDamageSyncWorkflow.cs");

        Assert.True(File.Exists(synchronizerPath), "Primaere-Schäden-Synchronisierung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(synchronizerFactoryPath), "Primaere-Schäden-Synchronisierung muss über Factory verdrahtet werden.");
        Assert.True(File.Exists(syncWorkflowPath), "Primaere-Schäden-Synchronisierung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        var protocol = File.ReadAllText(protocolPath);
        var policy = File.ReadAllText(policyPath);
        var synchronizer = File.ReadAllText(synchronizerPath);
        var synchronizerFactory = File.ReadAllText(synchronizerFactoryPath);
        var syncWorkflow = File.Exists(syncWorkflowPath) ? File.ReadAllText(syncWorkflowPath) : "";

        Assert.Contains("CodingPrimaryDamageSyncWorkflow.Sync", protocol);
        Assert.Contains("DataPageProtocolObservationMapper.BuildPrimaryDamageLines", policy);
        Assert.Contains("CodingPrimaryDamageTextBuilder.Build", synchronizerFactory);
        Assert.Contains("CodingPrimaryDamageSynchronizerFactory.Create", syncWorkflow);
        Assert.Contains("synchronizer.Sync(record, document)", syncWorkflow);
        Assert.Contains("SetFieldValue(\"Primaere_Schaeden\"", synchronizer);
    }

    [Fact]
    public void PlayerWindow_primary_damage_text_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var protocolPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Protocol.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingPrimaryDamageTextBuilder.cs");
        var synchronizerPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingPrimaryDamageSynchronizer.cs");
        var synchronizerFactoryPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingPrimaryDamageSynchronizerFactory.cs");
        var syncWorkflowPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingPrimaryDamageSyncWorkflow.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingPrimaryDamageSyncCommandWorkflow.cs");

        Assert.True(File.Exists(policyPath), "Primaere-Schäden-Textbildung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(synchronizerPath), "Primaere-Schäden-Feldschreiben muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(synchronizerFactoryPath), "Primaere-Schäden-Feldschreiben muss über Factory verdrahtet werden.");
        Assert.True(File.Exists(syncWorkflowPath), "Primaere-Schäden-Feldschreiben soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(commandWorkflowPath), "Primaere-Schäden-Sync-Gate muss ausserhalb der PlayerWindow-Partials liegen.");

        var protocol = File.ReadAllText(protocolPath);
        var policy = File.ReadAllText(policyPath);
        var synchronizer = File.ReadAllText(synchronizerPath);
        var synchronizerFactory = File.ReadAllText(synchronizerFactoryPath);
        var syncWorkflow = File.Exists(syncWorkflowPath) ? File.ReadAllText(syncWorkflowPath) : "";
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";

        Assert.Contains("CodingPrimaryDamageSyncCommandWorkflow.Execute", protocol);
        Assert.Contains("CodingPrimaryDamageSyncWorkflow.Sync", protocol);
        Assert.Contains("if (!request.HasHaltungRecord)", commandWorkflow);
        Assert.Contains("actions.SyncPrimaryDamages()", commandWorkflow);
        Assert.Contains("public static string Build", policy);
        Assert.Contains("DataPageProtocolObservationMapper.BuildPrimaryDamageLines", policy);
        Assert.Contains("SetFieldValue(\"Primaere_Schaeden\"", synchronizer);
        Assert.Contains("CodingPrimaryDamageTextBuilder.Build", synchronizerFactory);
        Assert.Contains("CodingPrimaryDamageSynchronizerFactory.Create", syncWorkflow);
        Assert.Contains("synchronizer.Sync(record, document)", syncWorkflow);
    }
}
