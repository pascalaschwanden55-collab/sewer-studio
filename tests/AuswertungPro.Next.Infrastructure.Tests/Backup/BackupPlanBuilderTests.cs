using System.IO;
using System.Linq;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

/// <summary>
/// Sicherungsplan: 5 Komponenten mit korrekten Ziel-Relativpfaden.
/// Arbeitet nur mit Fantasie-Pfaden — kein Dateisystemzugriff.
/// </summary>
public class BackupPlanBuilderTests
{
    private static FullBackupSources TestSources(string? repoRoot = @"X:\Repo") => new(
        RepoRoot: repoRoot,
        KnowledgeRoot: @"X:\Brain",
        LocalSewerStudioDir: @"X:\Local\SewerStudio",
        RoamingSewerStudioDir: @"X:\Roaming\SewerStudio",
        RoamingAuswertungProDir: @"X:\Roaming\AuswertungPro",
        DesktopDir: @"X:\Desktop",
        AppVersion: "4.4",
        EnvironmentVariables: new Dictionary<string, string>());

    [Fact]
    public void Build_LiefertFuenfKomponenten()
    {
        var plan = BackupPlanBuilder.Build(TestSources());

        Assert.Equal(5, plan.Count);
        Assert.Equal(
            new[] { "Programm", "KI-Gehirn", "Einstellungen", "Logs", "Extras" },
            plan.Select(k => k.Name).ToArray());
    }

    [Fact]
    public void Build_ZielRelativpfade_Korrekt()
    {
        var plan = BackupPlanBuilder.Build(TestSources());

        var programm = plan.Single(k => k.Name == "Programm");
        Assert.Equal("Programm", programm.Sources.Single().TargetRelativeRoot);
        Assert.Equal(@"X:\Repo", programm.Sources.Single().SourceRoot);

        var brain = plan.Single(k => k.Name == "KI-Gehirn");
        Assert.Equal("KI_BRAIN", brain.Sources.Single().TargetRelativeRoot);
        Assert.Equal(@"X:\Brain", brain.Sources.Single().SourceRoot);

        var einstellungen = plan.Single(k => k.Name == "Einstellungen");
        Assert.Equal(3, einstellungen.Sources.Count);
        Assert.Contains(einstellungen.Sources, s =>
            s.TargetRelativeRoot == Path.Combine("Einstellungen", "Local_SewerStudio"));
        Assert.Contains(einstellungen.Sources, s =>
            s.TargetRelativeRoot == Path.Combine("Einstellungen", "Roaming_SewerStudio"));
        Assert.Contains(einstellungen.Sources, s =>
            s.TargetRelativeRoot == Path.Combine("Einstellungen", "Roaming_AuswertungPro"));

        var logs = plan.Single(k => k.Name == "Logs");
        Assert.Equal(2, logs.Sources.Count);
        Assert.Contains(logs.Sources, s =>
            s.SourceRoot == Path.Combine(@"X:\Local\SewerStudio", "logs")
            && s.TargetRelativeRoot == Path.Combine("Logs", "logs"));
        Assert.Contains(logs.Sources, s =>
            s.SourceRoot == Path.Combine(@"X:\Local\SewerStudio", "Telemetry")
            && s.TargetRelativeRoot == Path.Combine("Logs", "Telemetry"));
    }

    [Fact]
    public void Build_RepoRootFehlt_ProgrammOhneQuellen()
    {
        var plan = BackupPlanBuilder.Build(TestSources(repoRoot: null));

        var programm = plan.Single(k => k.Name == "Programm");
        Assert.Empty(programm.Sources);
    }

    [Fact]
    public void Build_Extras_EnthaeltDesktopSkripte()
    {
        var plan = BackupPlanBuilder.Build(TestSources());

        var extras = plan.Single(k => k.Name == "Extras");
        Assert.Empty(extras.Sources);
        Assert.NotNull(extras.Files);
        Assert.Equal(3, extras.Files!.Count);
        Assert.Contains(extras.Files, f =>
            f.SourcePath == Path.Combine(@"X:\Desktop", "Backup_KI_BRAIN.bat")
            && f.TargetRelativePath == Path.Combine("Extras", "Backup_KI_BRAIN.bat"));
    }

    [Fact]
    public void Build_AusschlussPraedikate_Verdrahtet()
    {
        var plan = BackupPlanBuilder.Build(TestSources());

        var programm = plan.Single(k => k.Name == "Programm").Sources.Single();
        Assert.NotNull(programm.IsDirExcluded);
        Assert.True(programm.IsDirExcluded!("bin"));
        Assert.False(programm.IsDirExcluded!(".git"));

        var brain = plan.Single(k => k.Name == "KI-Gehirn").Sources.Single();
        Assert.True(brain.IsDirExcluded!("kb_backups"));
        Assert.False(brain.IsDirExcluded!("gold_labels"));

        var logs = plan.Single(k => k.Name == "Logs");
        Assert.All(logs.Sources, s => Assert.Null(s.IsDirExcluded));
    }
}
