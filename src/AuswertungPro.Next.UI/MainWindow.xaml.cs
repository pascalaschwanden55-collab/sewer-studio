using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AuswertungPro.Next.UI.Controls;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI;

public partial class MainWindow : Window
{
    private bool _isDataContextDisposed;
    private bool _startupEntrancePlayed;

    public MainWindow()
    {
        InitializeComponent();
        WindowStateManager.Track(this);
        var services = GetServiceProvider();
        // Toast-Senke mit dem sichtbaren Host verbinden (nicht-blockierende Erfolgsmeldungen).
        services.Toasts.AttachSink((message, severity) => ToastHostControl.Enqueue(message, severity));
        DataContext = new ShellViewModel(services);
    }

    public async Task PlayStartupEntranceAsync()
    {
        if (_startupEntrancePlayed || SidebarNavList.Visibility != Visibility.Visible)
            return;

        _startupEntrancePlayed = true;
        SidebarNavList.UpdateLayout();

        var containers = new ListBoxItem[SidebarNavList.Items.Count];
        for (var i = 0; i < containers.Length; i++)
        {
            if (SidebarNavList.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem item)
                continue;

            item.Opacity = 0;
            item.RenderTransform = new TranslateTransform(-8, 0);
            containers[i] = item;
        }

        for (var i = 0; i < containers.Length; i++)
        {
            var item = containers[i];
            if (item is null)
                continue;

            var fade = new DoubleAnimation(0, 1, AnimationTokens.Normal)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            item.BeginAnimation(OpacityProperty, fade);

            if (item.RenderTransform is TranslateTransform slide)
            {
                var move = new DoubleAnimation(-8, 0, AnimationTokens.Slow)
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                slide.BeginAnimation(TranslateTransform.XProperty, move);
            }

            await Task.Delay(30);
        }
    }

    private static ServiceProvider GetServiceProvider()
        => App.Services is ServiceProvider sp
            ? sp
            : throw new InvalidOperationException("ServiceProvider wurde nicht initialisiert.");

    private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is ShellViewModel closeVm && !ShellLeaveGuard.CanLeave(closeVm.CurrentPage))
        {
            e.Cancel = true;
            return;
        }

        if (DataContext is ShellViewModel vm && vm.Project.Dirty)
        {
            var result = DialogHost.Current.ConfirmCancel(
                "Es gibt ungespeicherte Änderungen. Jetzt speichern?",
                "Projekt speichern");

            if (result == DialogConfirm.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (result == DialogConfirm.Yes)
            {
                vm.TrySaveProject();
                if (vm.Project.Dirty)
                {
                    e.Cancel = true;
                    return;
                }
            }
        }

        DisposeDataContext();

        // App explizit beenden (ShutdownMode = OnExplicitShutdown)
        System.Windows.Application.Current.Shutdown();
    }

    private void DisposeDataContext()
    {
        if (_isDataContextDisposed)
            return;

        _isDataContextDisposed = true;
        if (DataContext is IDisposable disposable)
            disposable.Dispose();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OpenCodeCatalog_Click(object sender, RoutedEventArgs e)
    {
        var sp = GetServiceProvider();

        var window = new CodeCatalogEditorWindow
        {
            Owner = this
        };
        window.DataContext = new CodeCatalogEditorViewModel(sp.CodeCatalog, window);
        window.ShowDialog();
    }

    private void OpenTrainingCenter_Click(object sender, RoutedEventArgs e)
    {
        var window = new TrainingCenterWindow(GetServiceProvider()) { Owner = this };
        window.Show();
    }

    private async void StartAi_Click(object sender, RoutedEventArgs e)
    {
        var sp = GetServiceProvider();

        var shell = DataContext as ShellViewModel;
        shell?.SetStatus("Starte KI...");

        try
        {
            var result = await AiStartupService.StartAsync(sp.Settings);
            sp.Settings.SaveImmediate();

            shell?.SetStatus(result.HasWarnings ? "KI-Start mit Warnung" : "KI gestartet");
            if (result.HasWarnings)
                sp.Dialogs.Info(result.Summary, "KI starten");
        }
        catch (System.Exception ex)
        {
            shell?.SetStatus($"KI-Start fehlgeschlagen: {ex.Message}");
            sp.Dialogs.Error($"KI konnte nicht gestartet werden:\n{ex.Message}", "KI starten");
        }
    }

    private void OpenKarte_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.ShellViewModel shell)
            return;
        var window = new Views.Windows.KarteWindow
        {
            Owner = this,
            DataContext = new ViewModels.Pages.KarteViewModel(shell, GetServiceProvider())
        };
        window.Show();
    }

    private void OpenSystemMonitor_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ShellViewModel shell)
            return;

        var panel = new SystemMonitorPanel
        {
            DataContext = shell.Monitor,
            Margin = new Thickness(12)
        };

        var window = new Window
        {
            Title = "System-Monitor",
            Owner = this,
            Width = 420,
            Height = 520,
            MinWidth = 360,
            MinHeight = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = panel,
            Background = TryFindResource("BgBrush") as Brush ?? Background
        };

        WindowStateManager.Track(window);
        window.Show();
    }

}
