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
                new ComponentSize("KI-Gehirn", "Lernen", 2048, 3, SourceFound: false),
                new ComponentSize("Projekte", "Projektdateien (Videos enthalten: nein)", 0, 0, SourceFound: true)
            ],
            TotalBytes: 3072,
            TotalFiles: 5);

        var text = SettingsFullBackupPresentationBuilder.BuildConfirmText(report, @"D:\Backup\SewerStudio_Datensicherung");

        Assert.Contains("Diese Datensicherung erstellt einen inkrementellen Spiegel.", text);
        Assert.Contains("- Programm:", text);
        Assert.Contains("- KI-Gehirn:", text);
        Assert.Contains("3 Dateien (Quelle nicht gefunden)", text);
        Assert.Contains("Gesamter Datenbestand:", text);
        Assert.Contains("Neue und geänderte Dateien werden kopiert.", text);
        Assert.Contains("Unveränderte Dateien werden geprüft und weiterverwendet.", text);
        Assert.Contains("Datenbanken werden als Schnappschüsse gesichert.", text);
        Assert.Contains("Die Gesamtgrösse ist nicht der zusätzlich benötigte freie Speicherplatz.", text);
        Assert.Contains("Für Änderungen und vorübergehende Vorherkopien wird zusätzlicher Platz benötigt.", text);
        Assert.Contains("Hinzu kommen Datenbank-Schnappschüsse und eine Reserve.", text);
        Assert.Contains("5 Dateien", text);
        Assert.Contains(@"Ziel: D:\Backup\SewerStudio_Datensicherung", text);
        Assert.Contains("Gesichert wird nur der aktuelle Stand.", text);
        Assert.Contains("Dateien sollen nach einem erfolgreichen Lauf entfernt werden.", text);
        Assert.Contains("Bei blockierter Löschung können ältere Kopien bestehen bleiben.", text);
        Assert.Contains("Projekte enthalten: ja.", text);
        Assert.Contains("Videos enthalten: nein", text);
    }

    /// <summary>
    /// Die Abschlussphasen (Zielordner pruefen, Aufraeumen, Zaehlen) kennen ihre
    /// Gesamtzahl nicht im Voraus und melden deshalb FilesTotal = 0. Der alte Text
    /// haette daraus "45000 von 0 Dateien" gemacht — die Zahl, die dem Bearbeiter
    /// zeigen soll, dass der Lauf arbeitet, haette ihn stattdessen verunsichert.
    /// </summary>
    [Fact]
    public void BuildProgress_zeigt_bei_unbekannter_Gesamtzahl_keine_Null_als_Ziel()
    {
        var progress = new FullBackupProgress(
            Component: "Zielordner prüfen",
            CurrentFile: "45'000 geprüft",
            BytesDone: 0,
            BytesTotal: 0,
            FilesDone: 45000,
            FilesTotal: 0);

        var presentation = SettingsFullBackupPresentationBuilder.BuildProgress(progress);

        Assert.DoesNotContain("von 0 Dateien", presentation.StatusText);
        Assert.Contains("Zielordner prüfen", presentation.StatusText);
        Assert.Contains("45", presentation.StatusText);
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
        // Seit 23.09.2026 melden die Abschlussphasen FilesTotal = 0, weil sie ihre
        // Gesamtzahl nicht im Voraus kennen. "0 von 0 Dateien" waere dort irrefuehrend.
        Assert.Equal("Extras: 0 Einträge", result.StatusText);
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
