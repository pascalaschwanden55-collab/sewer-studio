using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Version 5.0 (Entscheid Pascal 13.09.2026): Der Startbildschirm zeigt die Version wieder, und zwar
/// aus <see cref="AppIdentity.DisplayVersion"/>. Ein reiner XAML-Textvergleich beweist nicht, dass
/// der x:Static-Verweis beim Aufbau wirklich aufgeloest wird - dieser Test baut das Fenster im
/// eigenen WPF-Prozess auf (ohne Show, ohne Timer-Start) und sucht den Chip im logischen Baum.
/// </summary>
[Collection("IsolatedWpf")]
public sealed class StartupSplashVersionIsolatedSmokeTests
{
    private static readonly string ChildTestName =
        typeof(StartupSplashVersionIsolatedSmokeTests).FullName + "." + nameof(Kindprozess_Splash_zeigt_die_Version_aus_AppIdentity);

    [Fact]
    public async Task Splash_laesst_sich_in_eigenem_Wpf_Prozess_pruefen()
    {
        Assert.Null(System.Windows.Application.Current);
        var result = await WpfIsolatedTestProcess.RunAsync(ChildTestName, TimeSpan.FromSeconds(60));
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess_Splash_zeigt_die_Version_aus_AppIdentity()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();

            var splash = new StartupSplashWindow();
            try
            {
                var texte = Alle<TextBlock>(splash).Select(t => t.Text).ToArray();
                Assert.Equal("v5.0", AppIdentity.DisplayVersion);
                Assert.Contains(AppIdentity.DisplayVersion, texte);
                Assert.Contains("VSA-KEK 2020", texte);
                // Genau ein Versions-Chip - keine zweite, anders formatierte Zahl daneben.
                Assert.Single(texte, t => t.StartsWith("v", StringComparison.Ordinal) && t.Contains('.'));
                WpfIsolatedTestProcess.MarkChildScenarioCompleted();
            }
            finally { splash.Close(); }
        });
    }

    private static IEnumerable<T> Alle<T>(DependencyObject wurzel) where T : DependencyObject
    {
        foreach (var kind in LogicalTreeHelper.GetChildren(wurzel).OfType<DependencyObject>())
        {
            if (kind is T t) yield return t;
            foreach (var enkel in Alle<T>(kind)) yield return enkel;
        }
    }
}
