using System.Windows;
using System.Windows.Media;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 13 (Windows-Integration): Hochkontrast-Ueberlagerung.
/// Laedt die echte <c>Theme/ThemeHighContrast.xaml</c> aus der echten Anwendungsressource -
/// braucht deshalb den echten WPF-Kindprozess (pack-URIs loesen ohne laufende
/// <see cref="System.Windows.Application"/> nicht auf).
/// </summary>
[Trait(TestKategorie.Name, TestKategorie.Kindprozess)]
[Collection("IsolatedWpf")]
public sealed class ThemeHighContrastIsolatedSmokeTests
{
    private static readonly string ChildTestName =
        typeof(ThemeHighContrastIsolatedSmokeTests).FullName
        + "."
        + nameof(Kindprozess_Ueberlagerung_ersetzt_kern_tokens_und_faellt_sonst_zurueck);

    [Fact]
    public async Task HighContrastOverlay_laesst_sich_in_eigenem_Wpf_Prozess_pruefen()
    {
        Assert.Null(System.Windows.Application.Current);
        var result = await WpfIsolatedTestProcess.RunAsync(ChildTestName, TimeSpan.FromSeconds(60));

        Assert.Null(System.Windows.Application.Current);
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess_Ueberlagerung_ersetzt_kern_tokens_und_faellt_sonst_zurueck()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new App { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();

            ThemeManager.ApplyTheme(app.Resources, ThemeManager.Light);
            Assert.False(ThemeManager.IsHighContrastOverlayApplied(app.Resources));

            var successBeforeHighContrast = Assert.IsType<SolidColorBrush>(app.Resources["SuccessBrush"]).Color;

            // Einschalten: die Kern-Tokens werden zu SystemColors, ein NICHT ueberlagerter
            // Schluessel (SuccessBrush ist bewusst NICHT in ThemeHighContrast.xaml enthalten)
            // faellt unveraendert auf den normalen Theme-Wert zurueck.
            ThemeManager.SetHighContrastOverlay(app.Resources, enabled: true);
            Assert.True(ThemeManager.IsHighContrastOverlayApplied(app.Resources));

            var textBrush = Assert.IsType<SolidColorBrush>(app.Resources["TextBrush"]);
            Assert.Equal(SystemColors.WindowTextColor, textBrush.Color);

            var accentBrush = Assert.IsType<SolidColorBrush>(app.Resources["AccentBrush"]);
            Assert.Equal(SystemColors.HighlightColor, accentBrush.Color);

            var successDuringHighContrast = Assert.IsType<SolidColorBrush>(app.Resources["SuccessBrush"]);
            Assert.Equal(successBeforeHighContrast, successDuringHighContrast.Color);

            // Erneutes Einschalten haengt die Ueberlagerung nicht doppelt ein.
            ThemeManager.SetHighContrastOverlay(app.Resources, enabled: true);
            var highContrastCount = app.Resources.MergedDictionaries
                .Count(d => (d.Source?.OriginalString ?? string.Empty).Replace('\\', '/')
                    .EndsWith("Theme/ThemeHighContrast.xaml", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(1, highContrastCount);

            // Ausschalten: TextBrush zeigt wieder den normalen (nicht system-gebundenen) Theme-Wert.
            ThemeManager.SetHighContrastOverlay(app.Resources, enabled: false);
            Assert.False(ThemeManager.IsHighContrastOverlayApplied(app.Resources));
            var textBrushAfter = Assert.IsType<SolidColorBrush>(app.Resources["TextBrush"]);
            Assert.NotEqual(SystemColors.WindowTextColor, textBrushAfter.Color);

            // Ein Design-Wechsel (Hell -> Dunkel) waehrend die Hochkontrast-Ueberlagerung aktiv
            // ist, darf sie nicht entfernen - sie ist von der Design-Wahl unabhaengig.
            ThemeManager.SetHighContrastOverlay(app.Resources, enabled: true);
            ThemeManager.ApplyTheme(app.Resources, ThemeManager.Dark);
            Assert.True(ThemeManager.IsHighContrastOverlayApplied(app.Resources));
            var textBrushAfterThemeSwitch = Assert.IsType<SolidColorBrush>(app.Resources["TextBrush"]);
            Assert.Equal(SystemColors.WindowTextColor, textBrushAfterThemeSwitch.Color);

            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
        });
    }
}
