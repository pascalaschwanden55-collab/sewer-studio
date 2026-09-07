using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova-Etappe 2b, Task 1: Der reine Text-Waechter <see cref="DesignAuditNovaTabelleTests"/>
/// prueft nur die XAML-Quelle. Dieser Test baut einen echten <see cref="DataGridColumnHeader"/>
/// mit den echten App-Ressourcen auf und belegt, dass die neue Vorlage tatsaechlich laedt (keine
/// XamlParseException ueber eine Ressource, die aus Theme.xaml/ThemeLight.xaml heraus nicht
/// gefunden wird) und einen String-Kopf sichtbar in Grossbuchstaben zeigt — in beiden Themes.
/// Laeuft wie die anderen WPF-Smoke-Tests in einem eigenen Kindprozess; kein Projekt, kein
/// ViewModel, kein Fensterstart.
/// </summary>
[Collection("IsolatedWpf")]
public sealed class DataGridColumnHeaderGrossbuchstabenIsolatedSmokeTests
{
    private static readonly string ChildTestName =
        typeof(DataGridColumnHeaderGrossbuchstabenIsolatedSmokeTests).FullName
        + "."
        + nameof(Kindprozess_Spaltenkopf_erscheint_gross_in_beiden_Themes);

    [Fact]
    public async Task Spaltenkopf_laesst_sich_in_eigenem_Wpf_Prozess_pruefen()
    {
        Assert.Null(System.Windows.Application.Current);
        var result = await WpfIsolatedTestProcess.RunAsync(ChildTestName, TimeSpan.FromSeconds(60));

        Assert.Null(System.Windows.Application.Current);
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess_Spaltenkopf_erscheint_gross_in_beiden_Themes()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();

            foreach (var theme in new[] { ThemeManager.Dark, ThemeManager.Light })
            {
                var file = theme == ThemeManager.Dark ? "Theme.xaml" : "ThemeLight.xaml";
                app.Resources.MergedDictionaries[0] = new ResourceDictionary
                {
                    Source = new Uri($"/SewerStudio;component/Theme/{file}", UriKind.Relative)
                };

                var header = new DataGridColumnHeader { Content = "strasse" };
                header.Measure(new Size(200, 40));
                header.Arrange(new Rect(0, 0, 200, 40));
                header.UpdateLayout();

                var text = Finde<TextBlock>(header);
                Assert.True(text is not null, $"{file}: keine TextBlock-Beschriftung im Spaltenkopf");
                Assert.Equal("STRASSE", text!.Text);
            }

            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
        });
    }

    private static T? Finde<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed)
                return typed;
            if (Finde<T>(child) is { } nested)
                return nested;
        }
        return null;
    }
}
