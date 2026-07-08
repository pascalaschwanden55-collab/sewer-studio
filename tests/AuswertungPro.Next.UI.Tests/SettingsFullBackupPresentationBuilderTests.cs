using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.UI.Settings;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SettingsFullBackupPresentationBuilderTests
{
    [Fact]
    public void BuildConfirmText_lists_components_totals_target_and_warnings()
    {
        var report = new FullBackupSizeReport(
            [
                new ComponentSize("Programm", "Code", 1024, 2, SourceFound: true),
                new ComponentSize("KI-Gehirn", "Lernen", 2048, 3, SourceFound: false)
            ],
            TotalBytes: 3072,
            TotalFiles: 5);

        var text = SettingsFullBackupPresentationBuilder.BuildConfirmText(report, @"D:\Backup\SewerStudio_Datensicherung");

        Assert.Contains("Diese Datensicherung erstellt einen inkrementellen Spiegel.", text);
        Assert.Contains("- Programm:", text);
        Assert.Contains("- KI-Gehirn:", text);
        Assert.Contains("3 Dateien (Quelle nicht gefunden)", text);
        Assert.Contains("Gesamt:", text);
        Assert.Contains("5 Dateien", text);
        Assert.Contains(@"Ziel: D:\Backup\SewerStudio_Datensicherung", text);
        Assert.Contains("_Versionen", text);
        Assert.Contains("Projekte und Videos sind nicht enthalten.", text);
    }

    [Fact]
    public void BuildProgress_clamps_percent_and_uses_current_file_name()
    {
        var progress = new FullBackupProgress(
            Component: "Programm",
            CurrentFile: @"C:\Quelle\sub\a.txt",
            BytesDone: 150,
            BytesTotal: 100,
            FilesDone: 4,
            FilesTotal: 10);

        var result = SettingsFullBackupPresentationBuilder.BuildProgress(progress);

        Assert.Equal(100, result.Percent);
        Assert.Equal("a.txt", result.CurrentFileName);
        Assert.Equal("Programm: 4 von 10 Dateien", result.StatusText);
    }

    [Fact]
    public void BuildProgress_returns_zero_percent_when_total_is_zero()
    {
        var result = SettingsFullBackupPresentationBuilder.BuildProgress(
            new FullBackupProgress("Extras", "", 10, 0, 0, 0));

        Assert.Equal(0, result.Percent);
        Assert.Equal("", result.CurrentFileName);
        Assert.Equal("Extras: 0 von 0 Dateien", result.StatusText);
    }

    [Fact]
    public void BuildLastBackupInfo_formats_missing_and_existing_backup_state()
    {
        Assert.Equal(
            "Noch keine Datensicherung erstellt.",
            SettingsFullBackupPresentationBuilder.BuildLastBackupInfo(null, null, null));

        var utc = new DateTime(2026, 7, 3, 12, 15, 0, DateTimeKind.Utc);
        var text = SettingsFullBackupPresentationBuilder.BuildLastBackupInfo(
            utc,
            @"E:\Backups",
            1024);

        Assert.Contains("Letzte Datensicherung:", text);
        Assert.Contains(utc.ToLocalTime().ToString("dd.MM.yyyy HH:mm"), text);
        Assert.Contains(@"E:\Backups", text);
    }
}
