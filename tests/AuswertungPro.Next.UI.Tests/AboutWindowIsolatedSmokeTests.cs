using System.IO;
using System.Windows;
using System.Windows.Controls;

using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 5 («Programmidentitaet»): «Über SewerStudio» wirklich in
/// einem eigenen WPF-Prozess bauen und pruefen (Muster wie <see cref="NovaDialogHeaderIsolatedSmokeTests"/>
/// und <see cref="DossierParcelLookupWindowKeyboardIsolatedSmokeTests"/>). Die Knopfregel selbst
/// (hoechstens ein Hauptknopf, IsCancel-Reihenfolge, kein lokaler Button-Style, kein Background=
/// direkt am Button) ist bereits durch den textbasierten <see cref="DesignAuditKnopfleistenTests"/>
/// abgedeckt, der ALLE Fenster-XAMLs im Projekt einschliesst - AboutWindow.xaml faellt automatisch
/// in diesen Umfang, ohne eigene Ausnahme.
///
/// Die «Öffnen»-Knoepfe werden hier bewusst NICHT geklickt: <c>SettingsPathWorkflow.OpenFolder</c>
/// oeffnet ueber den fest eingebauten <c>CompatibilityService</c> (<c>FolderOpenService</c> +
/// <c>SafeShellOpenService</c>) einen ECHTEN Explorer-Prozess - das ist Sache von
/// <c>SettingsPathWorkflowTests</c>, nicht dieses Fenstertests.
/// </summary>
[Collection("IsolatedWpf")]
public sealed class AboutWindowIsolatedSmokeTests
{
    [Fact]
    public async Task AboutWindow_laesst_sich_in_eigenem_Wpf_Prozess_pruefen()
    {
        var result = await WpfIsolatedTestProcess.RunAsync(
            typeof(AboutWindowIsolatedSmokeTests).FullName + "." + nameof(Kindprozess),
            TimeSpan.FromSeconds(60));
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0 && result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();

            var dialogs = new FakeDialogService();
            var fenster = new AboutWindow("RTX 5090 (Test)", dialogs);

            // Off-screen zeigen: die NovaDialogHeader-Vorlage loest ihren impliziten Stil erst als
            // Teil eines echten, angezeigten Fensters zuverlaessig auf (siehe
            // NovaDialogHeaderIsolatedSmokeTests). Bewusst KEIN
            // "Dispatcher.Invoke(…, DispatcherPriority.ApplicationIdle)" danach: AboutWindow traegt
            // wie die Knopfregel es verlangt "ui:WindowFx.Entrance=True", und dessen laufende
            // Eintritts-Animation kann einen danach eingeplanten Idle-Aufruf verhungern lassen -
            // derselbe dokumentierte Grund, aus dem BeobachtungenWindowIsolatedSmokeTests dort
            // ausschliesslich UpdateLayout()/IsLoaded verwendet.
            fenster.ShowActivated = false;
            fenster.ShowInTaskbar = false;
            fenster.WindowStartupLocation = WindowStartupLocation.Manual;
            fenster.Left = -20000;
            fenster.Top = -20000;
            fenster.Show();
            fenster.UpdateLayout();

            try
            {
                Assert.True(fenster.IsLoaded);
                Assert.True(fenster.IsVisible);
                Assert.Contains("SewerStudio", fenster.Title);

                var kopf = (AuswertungPro.Next.UI.Controls.NovaDialogHeader)fenster.FindName("Kopf");
                Assert.Equal("Über SewerStudio", kopf.Title);

                // ── Name/Version aus AppIdentity, nicht hartkodiert im Fenster ──
                var nameText = (TextBlock)fenster.FindName("NameText");
                var versionText = (TextBlock)fenster.FindName("VersionText");
                Assert.Equal(AppIdentity.ProductName, nameText.Text);
                Assert.Equal(AppIdentity.DisplayVersion, versionText.Text);

                // ── Build-Datum: aus dem Schreibzeitpunkt der Assembly, keine erfundene Angabe ──
                var buildText = (TextBlock)fenster.FindName("BuildText");
                Assert.Equal(Visibility.Visible, buildText.Visibility);
                Assert.StartsWith("Build: ", buildText.Text);

                // ── System: Windows/.NET immer gefuellt, GPU nur wenn uebergeben (hier ja) ──
                var osText = (TextBlock)fenster.FindName("OsText");
                var dotNetText = (TextBlock)fenster.FindName("DotNetText");
                var gpuText = (TextBlock)fenster.FindName("GpuText");
                Assert.StartsWith("Windows: ", osText.Text);
                Assert.StartsWith(".NET: ", dotNetText.Text);
                Assert.Equal(Visibility.Visible, gpuText.Visibility);
                Assert.Equal("GPU: RTX 5090 (Test)", gpuText.Text);

                // ── Ordner kommen aus AppSettings.AppDataDir, nicht hartkodiert ──
                var dataBox = (TextBox)fenster.FindName("DataFolderBox");
                var logsBox = (TextBox)fenster.FindName("LogsFolderBox");
                var settingsBox = (TextBox)fenster.FindName("SettingsFolderBox");
                Assert.Equal(AppSettings.AppDataDir, dataBox.Text);
                Assert.Equal(Path.Combine(AppSettings.AppDataDir, "logs"), logsBox.Text);
                Assert.Equal(AppSettings.AppDataDir, settingsBox.Text);

                // ── Programmsymbol: dasselbe geladene app.ico wie der Fenstertitel-Standard ──
                var icon = (Image)fenster.FindName("ProgrammIcon");
                Assert.NotNull(icon.Source);
                Assert.NotNull(fenster.Icon);

                // ── Echte Knopf-Verdrahtung: RaiseEvent statt den Handler direkt aufzurufen ──
                var copyButton = (Button)fenster.FindName("CopySystemInfoButton");
                copyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                // Ein gesperrtes Clipboard darf nicht zum Absturz fuehren (Aufgabe 1, gleiches
                // Muster wie NovaDialogWindow.OnKopieren) - dieser Aufruf muss ohne Ausnahme
                // zurueckkehren, unabhaengig vom tatsaechlichen Clipboard-Zustand der Testumgebung.

                var closeButton = (Button)fenster.FindName("CloseButton");
                Assert.True(closeButton.IsCancel);
                closeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.False(fenster.IsVisible, "Der Schliessen-Knopf muss das Fenster wirklich schliessen.");
            }
            finally
            {
                if (fenster.IsVisible)
                    fenster.Close();
            }

            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
            app.Shutdown();
        });
    }

    private sealed class FakeDialogService : IDialogService
    {
        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string[] OpenFiles(string title, string filter) => Array.Empty<string>();
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") { }
        public bool Confirm(string message, string title = "Bestätigung") => false;
        public bool ConfirmWarn(string message, string title = "Bestätigung", bool defaultNo = true) => false;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestätigung") => DialogConfirm.Cancel;
    }
}
