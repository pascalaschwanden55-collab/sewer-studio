using System.IO;
using System.Linq;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

/// <summary>
/// Sicherungsplan: 6 Komponenten mit korrekten Ziel-Relativpfaden.
/// Arbeitet nur mit Fantasie-Pfaden — kein Dateisystemzugriff.
/// </summary>
public class BackupPlanBuilderTests
{
    private static FullBackupSources TestSources(
        string? repoRoot = @"X:\Repo",
        IReadOnlyList<string>? projectRoots = null,
        IReadOnlyList<string>? optionalProjectRoots = null,
        bool includeProjectVideos = false) => new(
        RepoRoot: repoRoot,
        KnowledgeRoot: @"X:\Brain",
        LocalSewerStudioDir: @"X:\Local\SewerStudio",
        RoamingSewerStudioDir: @"X:\Roaming\SewerStudio",
        RoamingAuswertungProDir: @"X:\Roaming\AuswertungPro",
        DesktopDir: @"X:\Desktop",
        AppVersion: "4.4",
        EnvironmentVariables: new Dictionary<string, string>(),
        ProjectRoots: projectRoots,
        IncludeProjectVideos: includeProjectVideos,
        OptionalProjectRoots: optionalProjectRoots);

    [Fact]
    public void Build_LiefertSechsKomponenten()
    {
        var plan = BackupPlanBuilder.Build(TestSources());

        Assert.Equal(6, plan.Count);
        Assert.Equal(
            new[] { "Programm", "KI-Gehirn", "Projekte", "Einstellungen", "Logs", "Extras" },
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

    [Fact]
    public void Build_Projekte_FasstDoppelteUndVerschachtelteWurzelnZusammen()
    {
        var plan = BackupPlanBuilder.Build(TestSources(projectRoots:
        [
            @"X:\Projekte",
            @"X:\Projekte\ProjektA",
            @"X:\Projekte",
            @"Y:\Einzelprojekt"
        ]));

        var projects = plan.Single(k => k.Name == "Projekte");
        Assert.Equal(2, projects.Sources.Count);
        Assert.Contains(projects.Sources, s => s.SourceRoot == @"X:\Projekte");
        Assert.Contains(projects.Sources, s => s.SourceRoot == @"Y:\Einzelprojekt");
    }

    [Fact]
    public void Build_Projekte_VideosStandardmaessigAusgeschlossen_UndOptionalEnthalten()
    {
        var withoutVideos = BackupPlanBuilder.Build(TestSources(projectRoots: [@"X:\Projekte"]))
            .Single(k => k.Name == "Projekte").Sources.Single();
        Assert.NotNull(withoutVideos.IsFileExcluded);
        Assert.True(withoutVideos.IsFileExcluded!("Haltungen\\film.mp4"));
        Assert.False(withoutVideos.IsFileExcluded!("Projektdateien\\projekt.json"));

        var withVideos = BackupPlanBuilder.Build(TestSources(
                projectRoots: [@"X:\Projekte"],
                includeProjectVideos: true))
            .Single(k => k.Name == "Projekte").Sources.Single();
        Assert.Null(withVideos.IsFileExcluded);
    }

    [Fact]
    public void Build_Projekte_Pflichtwurzel_deckt_Unterordner_ab_und_alter_externer_Pfad_bleibt_optional()
    {
        var plan = BackupPlanBuilder.Build(TestSources(
            projectRoots: [@"X:\Projekte"],
            optionalProjectRoots:
            [
                @"X:\Projekte\AltesProjekt",
                @"Y:\NichtMehrVorhanden"
            ]));

        var projects = plan.Single(k => k.Name == "Projekte");
        Assert.Equal(2, projects.Sources.Count);
        var required = Assert.Single(projects.Sources, source => source.SourceRoot == @"X:\Projekte");
        Assert.True(required.Required);
        Assert.False(required.WarnIfMissing);
        var optional = Assert.Single(projects.Sources, source => source.SourceRoot == @"Y:\NichtMehrVorhanden");
        Assert.False(optional.Required);
        Assert.True(optional.WarnIfMissing);
    }
}
