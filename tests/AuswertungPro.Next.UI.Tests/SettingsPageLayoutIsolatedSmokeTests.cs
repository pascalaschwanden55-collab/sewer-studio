using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.UI.Views.Pages;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova-Fixwelle B8: Auf der Einstellungsseite stand zwischen Seitenkopf und der ersten Gruppe
/// „Darstellung und Diagnose" rund 200 px leere Fläche. Dieser Test baut die Seite mit den echten
/// App-Ressourcen auf und misst, wie weit die erste Gruppe unter dem Reiterbereich beginnt.
/// Läuft wie die anderen WPF-Smoke-Tests in einem eigenen Kindprozess.
/// </summary>
[Collection("IsolatedWpf")]
public sealed class SettingsPageLayoutIsolatedSmokeTests
{
    private static readonly string ChildTestName =
        typeof(SettingsPageLayoutIsolatedSmokeTests).FullName
        + "."
        + nameof(Kindprozess_Erste_Gruppe_beginnt_oben);

    [Fact]
    public async Task Einstellungsseite_laesst_sich_in_eigenem_Wpf_Prozess_pruefen()
    {
        Assert.Null(System.Windows.Application.Current);
        var result = await WpfIsolatedTestProcess.RunAsync(ChildTestName, TimeSpan.FromSeconds(60));

        Assert.Null(System.Windows.Application.Current);
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess_Erste_Gruppe_beginnt_oben()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();

            var page = new SettingsPage { DataContext = new object() };
            page.Measure(new Size(1660, 900));
            page.Arrange(new Rect(0, 0, 1660, 900));
            page.UpdateLayout();

            var tabs = Assert.IsType<TabControl>(page.FindName("EinstellungsReiter"));
            var gruppe = Finde<GroupBox>(tabs);
            Assert.NotNull(gruppe);

            // Ursache war ein mittig ausgerichteter Reiterinhalt (VerticalContentAlignment
            // aus dem Windows-Grundstil des TabControl).
            var oben = gruppe!.TransformToAncestor(tabs).Transform(new Point(0, 0)).Y;
            Assert.True(oben < 40, $"Die erste Einstellungsgruppe beginnt {oben:0} px unter dem Reiterbereich.");

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
