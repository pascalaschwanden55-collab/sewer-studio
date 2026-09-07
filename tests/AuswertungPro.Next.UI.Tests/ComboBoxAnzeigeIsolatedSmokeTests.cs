using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova-Fixwelle B5: In den Prüfhost-Bildern zeigten drei Auswahlfelder den Rohtext des
/// Optionsobjekts (<c>AutoSaveModeOption { Value = … }</c>) statt seiner Beschriftung, obwohl
/// im XAML überall <c>DisplayMemberPath</c> gesetzt ist.
///
/// Dieser Test baut eine ComboBox mit den ECHTEN Anwendungsressourcen (<see cref="App"/>) auf,
/// setzt <c>DisplayMemberPath</c> und eine Auswahl und liest danach den wirklich sichtbaren Text
/// aus dem fertig getemplateten Control. Er beantwortet damit, ob es ein Produktfehler ist oder
/// nur der Prüfhost. Läuft wie die anderen WPF-Smoke-Tests in einem eigenen Kindprozess.
/// </summary>
[Collection("IsolatedWpf")]
public sealed class ComboBoxAnzeigeIsolatedSmokeTests
{
    private sealed record Option(string Value, string Label);

    private static readonly string ChildTestName =
        typeof(ComboBoxAnzeigeIsolatedSmokeTests).FullName
        + "."
        + nameof(Kindprozess_ComboBox_zeigt_die_Beschriftung_statt_ToString);

    [Fact]
    public async Task ComboBox_Anzeige_laesst_sich_in_eigenem_Wpf_Prozess_pruefen()
    {
        Assert.Null(System.Windows.Application.Current);
        var result = await WpfIsolatedTestProcess.RunAsync(ChildTestName, TimeSpan.FromSeconds(60));

        Assert.Null(System.Windows.Application.Current);
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess_ComboBox_zeigt_die_Beschriftung_statt_ToString()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();

            var optionen = new List<Option> { new("A", "Bei jeder Änderung"), new("B", "Nur beim Schliessen") };

            // Reihenfolge wie im XAML der Einstellungen: ItemsSource, SelectedValuePath,
            // DisplayMemberPath, danach die Auswahl.
            var box = new ComboBox
            {
                ItemsSource = optionen,
                SelectedValuePath = nameof(Option.Value),
                DisplayMemberPath = nameof(Option.Label)
            };
            box.SelectedValue = "A";

            var host = new Border { Width = 260, Height = 40, Child = box };
            host.Measure(new Size(260, 40));
            host.Arrange(new Rect(0, 0, 260, 40));
            host.UpdateLayout();

            var sichtbar = SichtbarerText(box);
            Assert.Equal("Bei jeder Änderung", sichtbar);

            // Beleg der Ursache: WPF selbst leitet die Anzeigevorlage NICHT aus DisplayMemberPath ab.
            Assert.Null(box.SelectionBoxItemTemplate);

            // Gegenprobe: dieselbe Box ohne DisplayMemberPath zeigt erwartungsgemäss ToString.
            var roh = new ComboBox { ItemsSource = optionen };
            roh.SelectedIndex = 0;
            var rohHost = new Border { Width = 260, Height = 40, Child = roh };
            rohHost.Measure(new Size(260, 40));
            rohHost.Arrange(new Rect(0, 0, 260, 40));
            rohHost.UpdateLayout();
            Assert.Contains("Option", SichtbarerText(roh), StringComparison.Ordinal);

            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
        });
    }

    /// <summary>Der Text, den die geschlossene ComboBox wirklich zeichnet.</summary>
    private static string SichtbarerText(ComboBox box)
    {
        var texte = new List<string>();
        Sammle(box, texte);
        return string.Join(" ", texte).Trim();
    }

    private static void Sammle(DependencyObject element, ICollection<string> texte)
    {
        var count = VisualTreeHelper.GetChildrenCount(element);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(element, i);
            if (child is TextBlock { Text.Length: > 0 } block && block.Visibility == Visibility.Visible)
                texte.Add(block.Text);
            Sammle(child, texte);
        }
    }
}
