using System;
using System.Windows;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Settings;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 5 («Programmidentitaet»): «Über SewerStudio» - Programmsymbol,
/// Name, Version (<see cref="AppIdentity"/>), Build-Datum, Ordner fuer Daten/Protokolle/
/// Einstellungen mit «Öffnen»-Knoepfen und «Systeminfo kopieren». Die reinen Angaben kommen aus
/// <see cref="AboutInfoProvider"/> (WPF-frei, dadurch ohne WPF-Testprozess pruefbar); dieses
/// Fenster stellt sie nur dar. Kein ServiceProvider noetig - <paramref name="dialogs"/> folgt dem
/// bestehenden Muster <c>dialogs ?? new DialogService()</c> (siehe z. B.
/// <c>ProjectPageViewModel</c>), <paramref name="gpuName"/> kommt optional vom Aufrufer (z. B.
/// <c>SystemMonitorService.GpuName</c> der Shell) statt hier einen eigenen Sensor zu starten.
/// </summary>
public partial class AboutWindow : Window
{
    private readonly IDialogService _dialogs;
    private readonly AboutSystemInfo _info;

    public AboutWindow(string? gpuName = null, IDialogService? dialogs = null)
    {
        InitializeComponent();
        WindowStateManager.Track(this);

        _dialogs = dialogs ?? new DialogService();
        _info = AboutInfoProvider.Erstelle(gpuName);

        // Dasselbe geladene Programmsymbol wie der Fenstertitel-Standard (App.LoadDefaultWindowIcon),
        // hier zusaetzlich gross im Fensterinhalt gezeigt. Ein Setzen von Icon ueberstimmt den
        // spaeteren automatischen Standard aus App.ApplyDefaultWindowIcon (der nur greift, wenn
        // Icon noch null ist) nicht negativ - es ist derselbe Wert.
        var symbol = App.LoadDefaultWindowIcon();
        Icon = symbol;
        ProgrammIcon.Source = symbol;

        NameText.Text = _info.ProduktName;
        VersionText.Text = _info.Version;

        var hatBuildDatum = !string.IsNullOrWhiteSpace(_info.BuildDatum);
        BuildText.Text = hatBuildDatum ? $"Build: {_info.BuildDatum}" : string.Empty;
        BuildText.Visibility = hatBuildDatum ? Visibility.Visible : Visibility.Collapsed;

        OsText.Text = $"Windows: {_info.Betriebssystem}";
        DotNetText.Text = $".NET: {_info.DotNetVersion}";

        var hatGpu = !string.IsNullOrWhiteSpace(_info.GpuName);
        GpuText.Text = hatGpu ? $"GPU: {_info.GpuName}" : string.Empty;
        GpuText.Visibility = hatGpu ? Visibility.Visible : Visibility.Collapsed;

        DataFolderBox.Text = _info.DatenOrdner;
        LogsFolderBox.Text = _info.ProtokollOrdner;
        SettingsFolderBox.Text = _info.EinstellungenOrdner;
    }

    private void OnOpenDataFolder(object sender, RoutedEventArgs e)
        => SettingsPathWorkflow.OpenFolder(_info.DatenOrdner, _dialogs);

    private void OnOpenLogsFolder(object sender, RoutedEventArgs e)
        => SettingsPathWorkflow.OpenFolder(_info.ProtokollOrdner, _dialogs);

    private void OnOpenSettingsFolder(object sender, RoutedEventArgs e)
        => SettingsPathWorkflow.OpenFolder(_info.EinstellungenOrdner, _dialogs);

    private void OnCopySystemInfo(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_info.AlsKopierbarerText());
        }
        catch (Exception)
        {
            // Die Zwischenablage kann in mancher Umgebung gesperrt sein (z. B. eine
            // Remote-Sitzung ohne Weiterleitung) - das darf das Fenster nicht zum Absturz
            // bringen (gleiches Muster wie NovaDialogWindow.OnKopieren, Aufgabe 1).
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
