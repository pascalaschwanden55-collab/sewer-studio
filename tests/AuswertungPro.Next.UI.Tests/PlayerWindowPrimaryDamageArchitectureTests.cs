using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowPrimaryDamageArchitectureTests
{
    [Fact]
    public void PlayerWindow_coding_primary_damage_text_uses_existing_mapper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var protocolPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Protocol.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingPrimaryDamageTextBuilder.cs");
        var synchronizerPath = Path.Combine(uiRoot, "Ai", "CodingPrimaryDamageSynchronizer.cs");
        var synchronizerFactoryPath = Path.Combine(uiRoot, "Ai", "CodingPrimaryDamageSynchronizerFactory.cs");
        var syncWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingPrimaryDamageSyncWorkflow.cs");

        Assert.True(File.Exists(synchronizerPath), "Primaere-Schaeden-Synchronisierung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(synchronizerFactoryPath), "Primaere-Schaeden-Synchronisierung muss ueber Factory verdrahtet werden.");
        Assert.True(File.Exists(syncWorkflowPath), "Primaere-Schaeden-Synchronisierung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        var protocol = File.ReadAllText(protocolPath);
        var policy = File.ReadAllText(policyPath);
        var synchronizer = File.ReadAllText(synchronizerPath);
        var synchronizerFactory = File.ReadAllText(synchronizerFactoryPath);
        var syncWorkflow = File.Exists(syncWorkflowPath) ? File.ReadAllText(syncWorkflowPath) : "";

        Assert.DoesNotContain("CodingPrimaryDamageSynchronizerFactory.Create", protocol);
        Assert.Contains("CodingPrimaryDamageSyncWorkflow.Sync", protocol);
        Assert.DoesNotContain(".Sync(_haltungRecord!, doc)", protocol);
        Assert.DoesNotContain("CodingPrimaryDamageTextBuilder.Build", protocol);
        Assert.DoesNotContain("SetFieldValue(\"Primaere_Schaeden\"", protocol);
        Assert.Contains("DataPageProtocolObservationMapper.BuildPrimaryDamageLines", policy);
        Assert.Contains("CodingPrimaryDamageTextBuilder.Build", synchronizerFactory);
        Assert.Contains("CodingPrimaryDamageSynchronizerFactory.Create", syncWorkflow);
        Assert.Contains("synchronizer.Sync(record, document)", syncWorkflow);
        Assert.Contains("SetFieldValue(\"Primaere_Schaeden\"", synchronizer);
        Assert.DoesNotContain("new HashSet<string>", protocol);
        Assert.DoesNotContain("Q1={q1}", protocol);
        Assert.DoesNotContain("Q2={q2}", protocol);
    }

    [Fact]
    public void PlayerWindow_primary_damage_text_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var protocolPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Protocol.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingPrimaryDamageTextBuilder.cs");
        var synchronizerPath = Path.Combine(uiRoot, "Ai", "CodingPrimaryDamageSynchronizer.cs");
        var synchronizerFactoryPath = Path.Combine(uiRoot, "Ai", "CodingPrimaryDamageSynchronizerFactory.cs");
        var syncWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingPrimaryDamageSyncWorkflow.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingPrimaryDamageSyncCommandWorkflow.cs");

        Assert.True(File.Exists(policyPath), "Primaere-Schaeden-Textbildung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(synchronizerPath), "Primaere-Schaeden-Feldschreiben muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(synchronizerFactoryPath), "Primaere-Schaeden-Feldschreiben muss ueber Factory verdrahtet werden.");
        Assert.True(File.Exists(syncWorkflowPath), "Primaere-Schaeden-Feldschreiben soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(commandWorkflowPath), "Primaere-Schaeden-Sync-Gate muss ausserhalb der PlayerWindow-Partials liegen.");

        var protocol = File.ReadAllText(protocolPath);
        var policy = File.ReadAllText(policyPath);
        var synchronizer = File.ReadAllText(synchronizerPath);
        var synchronizerFactory = File.ReadAllText(synchronizerFactoryPath);
        var syncWorkflow = File.Exists(syncWorkflowPath) ? File.ReadAllText(syncWorkflowPath) : "";
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";

        Assert.Contains("CodingPrimaryDamageSyncCommandWorkflow.Execute", protocol);
        Assert.DoesNotContain("CodingPrimaryDamageSynchronizerFactory.Create", protocol);
        Assert.Contains("CodingPrimaryDamageSyncWorkflow.Sync", protocol);
        Assert.DoesNotContain(".Sync(_haltungRecord!, doc)", protocol);
        Assert.DoesNotContain("if (_haltungRecord == null) return", protocol);
        Assert.DoesNotContain("CodingPrimaryDamageTextBuilder.Build", protocol);
        Assert.DoesNotContain("SetFieldValue(\"Primaere_Schaeden\"", protocol);
        Assert.DoesNotContain("DataPageProtocolObservationMapper.BuildPrimaryDamageLines", protocol);
        Assert.Contains("if (!request.HasHaltungRecord)", commandWorkflow);
        Assert.Contains("actions.SyncPrimaryDamages()", commandWorkflow);
        Assert.Contains("public static string Build", policy);
        Assert.Contains("SetFieldValue(\"Primaere_Schaeden\"", synchronizer);
        Assert.Contains("CodingPrimaryDamageTextBuilder.Build", synchronizerFactory);
        Assert.Contains("CodingPrimaryDamageSynchronizerFactory.Create", syncWorkflow);
        Assert.Contains("synchronizer.Sync(record, document)", syncWorkflow);
    }
}
