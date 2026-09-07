using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.UI.Controls;
using AuswertungPro.Next.UI.Views.Pages;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Fix-Runde 1 zu Task 18: Kein bisheriger Test hatte tatsaechlich eine der auf
/// <see cref="NovaPageHeader"/> umgestellten Seiten aufgebaut. Baut SettingsPage und
/// BuilderPage mit den echten App-Ressourcen auf (Kopie des Musters aus
/// <see cref="DataPageNovaLayoutIsolatedSmokeTests"/>) und prueft am fertig getemplateten
/// Control: Titel sichtbar mit erwartetem Text, Untertitel-Kollaps korrekt je Seite, und der
/// Aktionen-Inhalt haengt tatsaechlich am DataContext der Seite (nicht an einer isolierten
/// eigenen Namescope). Laeuft wie die anderen WPF-Smoke-Tests in einem eigenen Kindprozess;
/// kein Projekt, kein echtes ViewModel, kein Fensterstart.
/// </summary>
[Collection("IsolatedWpf")]
public sealed class NovaPageHeaderIsolatedSmokeTests
{
    private static readonly string ChildTestName =
        typeof(NovaPageHeaderIsolatedSmokeTests).FullName
        + "."
        + nameof(Kindprozess_SettingsPage_und_BuilderPage_zeigen_den_Seitenkopf_korrekt);

    [Fact]
    public async Task NovaPageHeader_laesst_sich_in_eigenem_Wpf_Prozess_pruefen()
    {
        Assert.Null(System.Windows.Application.Current);
        var result = await WpfIsolatedTestProcess.RunAsync(ChildTestName, TimeSpan.FromSeconds(60));

        Assert.Null(System.Windows.Application.Current);
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess_SettingsPage_und_BuilderPage_zeigen_den_Seitenkopf_korrekt()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();

            // SettingsPage: Titel ohne Untertitel - der Untertitel-TextBlock muss kollabieren.
            var settingsContext = new object();
            var settingsPage = new SettingsPage { DataContext = settingsContext };
            Layout(settingsPage);

            var settingsHeader = FindDescendant<NovaPageHeader>(settingsPage);
            Assert.NotNull(settingsHeader);
            settingsHeader!.ApplyTemplate();

            // Fix-Runde 1, Punkt 1: Ein Control ist per Default fokussierbar - der Seitenkopf
            // selbst darf keinen Tab-Stopp/Fokusrahmen erzeugen.
            Assert.False(settingsHeader.Focusable, "NovaPageHeader darf nicht fokussierbar sein.");
            Assert.False(settingsHeader.IsTabStop, "NovaPageHeader darf kein Tab-Stopp sein.");

            var settingsTitle = Assert.IsType<TextBlock>(settingsHeader.Template.FindName("TitleText", settingsHeader));
            Assert.Equal("Einstellungen", settingsTitle.Text);
            Assert.Equal(Visibility.Visible, settingsTitle.Visibility);

            var settingsSubtitle = Assert.IsType<TextBlock>(settingsHeader.Template.FindName("SubtitleText", settingsHeader));
            Assert.Equal(Visibility.Collapsed, settingsSubtitle.Visibility);

            // Der Aktionen-Inhalt (Suchfeld + Speichern-Knopf) muss am Seiten-DataContext haengen,
            // nicht null und nicht an einer eigenen, isolierten Namescope des Controls.
            var settingsAktionen = Assert.IsType<StackPanel>(settingsHeader.Aktionen);
            Assert.NotNull(settingsAktionen.DataContext);
            Assert.Same(settingsContext, settingsAktionen.DataContext);
            var sucheBox = Assert.IsType<TextBox>(settingsPage.FindName("SucheBox"));
            Assert.Same(settingsContext, sucheBox.DataContext);

            // BuilderPage: Titel MIT Untertitel - hier muss der Untertitel-TextBlock sichtbar sein,
            // und die per x:Name referenzierte ComboBox im Aktionen-Inhalt muss dieselbe Namescope
            // wie die Seite teilen (das war der urspruengliche MC3093-Build-Fehler).
            var builderContext = new object();
            var builderPage = new BuilderPage { DataContext = builderContext };
            Layout(builderPage);

            var builderHeader = FindDescendant<NovaPageHeader>(builderPage);
            Assert.NotNull(builderHeader);
            builderHeader!.ApplyTemplate();

            var builderTitle = Assert.IsType<TextBlock>(builderHeader.Template.FindName("TitleText", builderHeader));
            Assert.Equal("Druckcenter", builderTitle.Text);
            Assert.Equal(Visibility.Visible, builderTitle.Visibility);

            var builderSubtitle = Assert.IsType<TextBlock>(builderHeader.Template.FindName("SubtitleText", builderHeader));
            Assert.Equal("Listen, Statistik und NPK-Leistungsverzeichnis", builderSubtitle.Text);
            Assert.Equal(Visibility.Visible, builderSubtitle.Visibility);

            var savedViewsBox = Assert.IsType<ComboBox>(builderPage.FindName("SavedViewsBox"));
            Assert.NotNull(savedViewsBox.DataContext);
            Assert.Same(builderContext, savedViewsBox.DataContext);

            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
        });
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed)
                return typed;
            if (FindDescendant<T>(child) is { } nested)
                return nested;
        }
        return null;
    }

    private static void Layout(UIElement element)
    {
        element.Measure(new Size(1400, 900));
        element.Arrange(new Rect(0, 0, 1400, 900));
        element.UpdateLayout();
    }
}
