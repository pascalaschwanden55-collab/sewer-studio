using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Schlusswelle (Item 1): belegt am echten WPF-Kindprozess, dass ein
/// <c>DynamicResource</c>-Theme-Token INNERHALB eines <c>VisualBrush.Visual</c>-Teilbaums (das
/// Leerzustands-Wasserzeichen von <c>MeasureTemplateEditorWindow.xaml</c>) sich aufloest UND einem
/// Theme-Wechsel folgt. Fix-Runde 2 (Aufgabe 13) liess die Stelle bewusst bei
/// <c>StaticResource</c>, weil dieser Nachweis fehlte - er ist hier nachgeholt.
/// </summary>
[Collection("IsolatedWpf")]
public sealed class MeasureTemplateEditorWindowWasserzeichenIsolatedSmokeTests
{
    private static readonly string ChildTestName =
        typeof(MeasureTemplateEditorWindowWasserzeichenIsolatedSmokeTests).FullName
        + "." + nameof(Kindprozess_Wasserzeichen_folgt_Theme);

    [Fact]
    public async Task Wasserzeichen_folgt_Theme_in_eigenem_Wpf_Prozess()
    {
        Assert.Null(System.Windows.Application.Current);
        var result = await WpfIsolatedTestProcess.RunAsync(ChildTestName, TimeSpan.FromSeconds(60));

        Assert.Null(System.Windows.Application.Current);
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess_Wasserzeichen_folgt_Theme()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();
            ThemeManager.ApplyTheme(app.Resources, ThemeManager.Light);

            // Nachbau derselben Struktur wie in MeasureTemplateEditorWindow.xaml: TextBlock mit
            // DynamicResource-Foreground als Wurzel eines VisualBrush.Visual, das wiederum als
            // Background eines echten, im Fenster sichtbaren Borders haengt.
            var flaeche = (Border)XamlReader.Parse("""
                <Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                        Width="120" Height="24">
                  <Border.Background>
                    <VisualBrush Stretch="None" AlignmentX="Left">
                      <VisualBrush.Visual>
                        <TextBlock x:Name="Wasserzeichen" Text="Suchen..." Foreground="{DynamicResource MutedBrush}" Margin="4,0,0,0"/>
                      </VisualBrush.Visual>
                    </VisualBrush>
                  </Border.Background>
                </Border>
                """);

            var fenster = new Window
            {
                Content = flaeche, Width = 200, Height = 100, Left = -20000, Top = -20000,
                ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None,
            };
            WindowFx.SetEntrance(fenster, false);
            fenster.Show();
            fenster.UpdateLayout();

            var wasserzeichen = (TextBlock)((VisualBrush)flaeche.Background).Visual;
            Color HelleMutedFarbe() => ((SolidColorBrush)app.Resources["MutedBrush"]).Color;

            // Aufloesung ueberhaupt: keine schwarze/erfundene Ersatzfarbe, sondern der echte Token.
            var helleFarbe = Assert.IsType<SolidColorBrush>(wasserzeichen.Foreground).Color;
            Assert.Equal(HelleMutedFarbe(), helleFarbe);

            // Themewechsel: derselbe eingehaengte Teilbaum muss ohne Neuaufbau mitziehen.
            ThemeManager.ApplyTheme(app.Resources, ThemeManager.Dark);
            fenster.UpdateLayout();
            Color DunkleMutedFarbe() => ((SolidColorBrush)app.Resources["MutedBrush"]).Color;
            var dunkleFarbe = Assert.IsType<SolidColorBrush>(wasserzeichen.Foreground).Color;
            Assert.Equal(DunkleMutedFarbe(), dunkleFarbe);
            Assert.NotEqual(helleFarbe, dunkleFarbe);

            fenster.Close();
            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
        });
    }
}
