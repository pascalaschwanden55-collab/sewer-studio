using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjektEroeffnungSettingsGuardTests
{
    private static string Vm()
        => File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "SettingsPageViewModel.cs"));

    private static string Xaml()
        => File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Pages", "SettingsPage.xaml"));

    [Fact]
    public void Vm_exposes_and_persists_projects_root()
    {
        var vm = Vm();
        Assert.Contains("ProjectsRootDirectory", vm);
        Assert.Contains("BrowseProjectsRootCommand", vm);
    }

    [Fact]
    public void Xaml_has_projects_root_field()
    {
        var xaml = Xaml();
        Assert.Contains("Projekte-Verzeichnis", xaml);
        Assert.Contains("BrowseProjectsRootCommand", xaml);
    }

    [Fact]
    public void Vm_exposes_abwasserkataster_xtf_path()
    {
        var vm = Vm();
        Assert.Contains("AbwasserkatasterXtfPath", vm);
        Assert.Contains("BrowseAbwasserkatasterXtfPathCommand", vm);
    }

    [Fact]
    public void Xaml_has_abwasserkataster_xtf_path_field()
    {
        var xaml = Xaml();
        Assert.Contains("Kataster-XTF (*.xtf)", xaml);
        Assert.Contains("AbwasserkatasterXtfPath", xaml);
        Assert.Contains("BrowseAbwasserkatasterXtfPathCommand", xaml);
    }

    [Fact]
    public void Vm_exposes_kanton_uri_xtf_directory()
    {
        var vm = Vm();
        Assert.Contains("KantonUriXtfDirectory", vm);
        Assert.Contains("BrowseKantonUriXtfDirectoryCommand", vm);
    }

    [Fact]
    public void Xaml_has_kanton_uri_xtf_directory_field()
    {
        var xaml = Xaml();
        Assert.Contains("XTF Kanton Uri", xaml);
        Assert.Contains("Leitungen", xaml);
        Assert.Contains("KantonUriXtfDirectory", xaml);
        Assert.Contains("BrowseKantonUriXtfDirectoryCommand", xaml);
    }

    [Fact]
    public void Xaml_groups_settings_by_topic()
    {
        var xaml = Xaml();
        Assert.Contains("Header=\"Allgemein\"", xaml);
        Assert.Contains("Header=\"Dateien und Ordner\"", xaml);
        Assert.Contains("Header=\"Import und Referenzdaten\"", xaml);
        Assert.Contains("Header=\"Video und KI\"", xaml);
        Assert.Contains("Header=\"Datensicherung\"", xaml);
        Assert.Contains("Header=\"Hilfe\"", xaml);
        Assert.Contains("Header=\"Projektdateien\"", xaml);
        Assert.Contains("Header=\"Datenordner und Logs\"", xaml);
        Assert.Contains("Header=\"Wiederherstellung\"", xaml);
        Assert.Contains("Header=\"Importquellen\"", xaml);
        Assert.Contains("Header=\"Referenzdaten\"", xaml);
        Assert.Contains("Header=\"Werkzeuge\"", xaml);
        Assert.Contains("Header=\"Video-Player\"", xaml);
        Assert.Contains("Header=\"KI-Laufzeit\"", xaml);
        Assert.Contains("settings:SettingsNavigation.Hint=\"Design, Diagnose, Speichern\"", xaml);
        Assert.Contains("settings:SettingsNavigation.Hint=\"Projekte, Logs, Wiederherstellung\"", xaml);
        Assert.Contains("settings:SettingsNavigation.Hint=\"Videos, PDF, XTF\"", xaml);
        Assert.Contains("settings:SettingsNavigation.Hint=\"Wiedergabe, KI-Start, Schwellen\"", xaml);
        Assert.Contains("settings:SettingsNavigation.Hint=\"KI-Wissen, PC-Schutz\"", xaml);
        Assert.Contains("settings:SettingsNavigation.Hint=\"Handbuch, Kurzbefehle\"", xaml);
        Assert.Contains("settings:SettingsNavigation.Index=\"01\"", xaml);
        Assert.Contains("settings:SettingsNavigation.Index=\"06\"", xaml);
        Assert.DoesNotContain("<TabItem Header=\"Projektpfade\"", xaml);
        Assert.DoesNotContain("<TabItem Header=\"Programmdaten\"", xaml);
        Assert.DoesNotContain("<TabItem Header=\"Importquellen\"", xaml);
        Assert.DoesNotContain("<TabItem Header=\"Referenzdaten\"", xaml);
        Assert.DoesNotContain("<TabItem Header=\"Video-Player\"", xaml);
        Assert.DoesNotContain("<TabItem Header=\"KI-Laufzeit\"", xaml);
        Assert.DoesNotContain("<TabItem Header=\"Backup\"", xaml);
        Assert.DoesNotContain("Header=\"Projekte &amp; Ordner\"", xaml);
        Assert.DoesNotContain("Header=\"Speichern und Sicherheit\"", xaml);
    }

    [Fact]
    public void SettingsPageViewModel_uses_shared_full_backup_state()
    {
        var vm = Vm();

        Assert.Contains("FullBackupOperation", vm);
        Assert.DoesNotContain("SettingsFullBackupPresentationBuilder", vm);
        Assert.DoesNotContain("BuildFullBackupConfirmText", vm);
        Assert.DoesNotContain("BuildLastFullBackupInfo", vm);
        Assert.DoesNotContain("ByteSizeFormatter", vm);
    }

    [Fact]
    public void SettingsPageViewModel_delegates_full_backup_workflow()
    {
        var vm = Vm();

        Assert.Contains("SettingsFullBackupWorkflow.RunAsync", vm);
        Assert.DoesNotContain("Zielordner fuer die Datensicherung waehlen", vm);
        Assert.DoesNotContain("_sp.FullBackup.AnalyzeAsync", vm);
        Assert.DoesNotContain("_sp.FullBackup.RunAsync", vm);
        Assert.DoesNotContain("BackupPlanBuilder.TargetFolderName", vm);
        Assert.DoesNotContain("SkippedFiles", vm);
    }

    [Fact]
    public void SettingsPageViewModel_delegates_knowledge_backup_workflow()
    {
        var vm = Vm();

        Assert.Contains("SettingsKnowledgeBackupWorkflow.ExportAsync", vm);
        Assert.Contains("SettingsKnowledgeBackupWorkflow.ImportAsync", vm);
        Assert.DoesNotContain("KnowledgeBackupService", vm);
        Assert.DoesNotContain("KI-Wissen erfolgreich exportiert", vm);
        Assert.DoesNotContain("Vorhandene KI-Daten", vm);
    }

    [Fact]
    public void SettingsPageViewModel_delegates_ai_startup_workflow()
    {
        var vm = Vm();

        Assert.Contains("SettingsAiStartupWorkflow.RunAsync", vm);
        Assert.DoesNotContain("AiStartupService.StartAsync", vm);
        Assert.DoesNotContain("KI-Start fehlgeschlagen", vm);
        Assert.DoesNotContain("KI-Start mit Warnung", vm);
        Assert.DoesNotContain("Starte KI...", vm);
    }

    [Fact]
    public void SettingsPageViewModel_delegates_path_workflow()
    {
        var vm = Vm();

        Assert.Contains("SettingsPathWorkflow", vm);
        Assert.DoesNotContain("Directory.CreateDirectory", vm);
        Assert.DoesNotContain("SafeShellOpen.TryOpen", vm);
        Assert.DoesNotContain("pdftotext.exe waehlen", vm);
        Assert.DoesNotContain("Projektpfad waehlen", vm);
        Assert.DoesNotContain("XTF-Ordner Kanton Uri waehlen", vm);
    }

    [Fact]
    public void SettingsPageViewModel_delegates_save_workflow()
    {
        var vm = Vm();

        Assert.Contains("SettingsSaveWorkflow.Save", vm);
        Assert.DoesNotContain("_sp.Settings.EnableDiagnostics =", vm);
        Assert.DoesNotContain("_sp.Settings.PdfToTextPath =", vm);
        Assert.DoesNotContain("_sp.Settings.LastProjectPath =", vm);
        Assert.DoesNotContain("_sp.Settings.VideoOutput =", vm);
        Assert.DoesNotContain("_sp.Diagnostics.EnableDiagnostics =", vm);
        Assert.DoesNotContain("_sp.Diagnostics.ExplicitPdfToTextPath =", vm);
    }

    [Fact]
    public void SettingsPageViewModel_delegates_theme_workflow()
    {
        var vm = Vm();

        Assert.Contains("SettingsThemeWorkflow.SyncUiThemeChanged", vm);
        Assert.Contains("SettingsThemeWorkflow.SyncIsDarkThemeChanged", vm);
        Assert.Contains("SettingsThemeWorkflow.ApplyTheme", vm);
        Assert.DoesNotContain("_sp.Settings.UiTheme =", vm);
        Assert.DoesNotContain("System.Windows.Application.Current", vm);
        Assert.DoesNotContain("ThemeManager.ApplyTheme", vm);
    }
}
