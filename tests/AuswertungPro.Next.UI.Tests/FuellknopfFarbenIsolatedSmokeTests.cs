using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 13, Fix-Runde 4: prueft die Farbzustaende der gefuellten Knoepfe,
/// Chips, Baum-Auswahl und die beiden Laufzeitmechanismen, auf denen die Korrekturen beruhen, mit den
/// ECHTEN Anwendungsressourcen im eigenen WPF-Kindprozess (IsMouseOver laesst sich nicht setzen -
/// Hover pruefen die statischen Farbpaare in <see cref="ThemeHighContrastFarbpaarTests"/>).
///
/// Belegt wird unter anderem, was die Runde-3-Pruefung nicht sah: Ein eigener TextBlock im Inhalt eines
/// PrimaryButton war TextBrush (#FF14213A, dunkles Marineblau auf Akzentblau) - der B7-Block im
/// ContentPresenter erreicht ihn nicht, erst der Stil in Style.Resources.
/// </summary>
[Collection("IsolatedWpf")]
public sealed class FuellknopfFarbenIsolatedSmokeTests
{
    private static readonly string ChildTestName =
        typeof(FuellknopfFarbenIsolatedSmokeTests).FullName + "." + nameof(Kindprozess_Farbzustaende);

    [Fact]
    public async Task Farbzustaende_lassen_sich_in_eigenem_Wpf_Prozess_pruefen()
    {
        Assert.Null(System.Windows.Application.Current);
        var result = await WpfIsolatedTestProcess.RunAsync(ChildTestName, TimeSpan.FromSeconds(60));

        Assert.Null(System.Windows.Application.Current);
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess_Farbzustaende()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();
            ThemeManager.ApplyTheme(app.Resources, ThemeManager.Light);

            // Alles in EINEM echten Fenster: Aenderungen an den App-Ressourcen (Hochkontrast
            // einschalten) erreichen nur Elemente in einem Fenster - lose Elemente behielten den alten
            // DynamicResource-Wert und der Test waere blind.
            var ablage = new StackPanel();
            var fenster = new Window
            {
                Content = ablage, Width = 400, Height = 900, Left = -20000, Top = -20000,
                ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None,
            };
            WindowFx.SetEntrance(fenster, false);
            fenster.Show();
            void Lege(FrameworkElement element)
            {
                ablage.Children.Add(new Border { Width = 320, Height = 80, Child = element });
                fenster.UpdateLayout();
            }

            Color Farbe(string key) => Assert.IsType<SolidColorBrush>(app.Resources[key]).Color;

            // ── PrimaryButton: Inhalt, Ruhe, Druck ───────────────────────────────────────────
            var (primary, primaryText) = Knopf("PrimaryButton");
            Assert.Equal(Farbe("OnAccentBrush"), TextFarbe(primaryText));
            Assert.Equal(Farbe("AccentBrush"), Flaeche(primary, "MainBorder"));
            Druecke(primary, true);
            Assert.Equal(Farbe("AccentPressedBrush"), Flaeche(primary, "MainBorder"));
            Assert.Equal(Color.FromRgb(0x1E, 0x40, 0xAF), Farbe("AccentPressedBrush"));

            // ── SuccessButton: eigener Druckton trotz BasedOn PrimaryButton ─────────────────
            var (success, successText) = Knopf("SuccessButton");
            Assert.Equal(Farbe("OnAccentBrush"), TextFarbe(successText));
            Assert.Equal(Farbe("SuccessBrush"), Flaeche(success, "MainBorder"));
            Druecke(success, true);
            Assert.Equal(Farbe("SuccessFillPressedBrush"), Flaeche(success, "MainBorder"));

            // ── ToolbarButtonAccent (helles Theme): Verlauf in Ruhe, Druckton unveraendert ─────
            var (toolbar, toolbarText) = Knopf("ToolbarButtonAccent");
            Assert.Equal(Farbe("OnAccentBrush"), TextFarbe(toolbarText));
            var verlauf = Assert.IsType<LinearGradientBrush>(Teil<Border>(toolbar, "bd").Background);
            Assert.Equal(Color.FromRgb(0x25, 0x63, 0xEB), verlauf.GradientStops[0].Color);
            Assert.Equal(Color.FromRgb(0x08, 0x91, 0xB2), verlauf.GradientStops[1].Color);
            Druecke(toolbar, true);
            Assert.Equal(Color.FromRgb(0x1E, 0x40, 0xAF), Assert.IsType<SolidColorBrush>(Teil<Border>(toolbar, "bd").Background).Color);

            // ── Spalten-Chip: eigener TextBlock folgt der Auswahl ───────────────────────────
            var chipText = new TextBlock { Text = "Kompakt" };
            var chip = new ToggleButton { Style = (Style)app.Resources["CompactToggleButton"], Content = new StackPanel { Children = { chipText } } };
            Lege(chip);
            Assert.Equal(Farbe("TextBrush"), TextFarbe(chipText));
            chip.IsChecked = true;
            chip.UpdateLayout();
            Assert.Equal(Farbe("SelectionTextBrush"), TextFarbe(chipText));

            // ── Baum-Auswahl: Rahmen nur unter Hochkontrast, nichts verschiebt sich ─────────
            var item = new TreeViewItem { Header = "Knoten" };
            var baum = new TreeView { Items = { item } };
            Lege(baum);
            item.IsSelected = true;
            item.UpdateLayout();
            Assert.True(item.IsSelected);
            Assert.Equal(Farbe("AccentSubtleBrush"), Assert.IsType<SolidColorBrush>(Teil<Border>(item, "ItemBorder").Background).Color);
            Assert.Equal(new Thickness(0), Teil<Border>(item, "ItemBorder").BorderThickness);
            Assert.Equal(Colors.Transparent, Assert.IsType<SolidColorBrush>(Teil<Border>(item, "HcAuswahlRahmen").BorderBrush).Color);

            // ── Laufzeitmechanismus 1: Color="{DynamicResource ColorAccent}" in einer lokalen
            //    Ressource (FotoAkzentBrush) folgt dem Theme, aber NICHT der Hochkontrast-Ueberlagerung.
            var fotoHost = (Border)XamlReader.Parse("""
                <Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                  <Border.Resources>
                    <SolidColorBrush x:Key="FotoAkzentBrush" Color="{DynamicResource ColorAccent}"/>
                  </Border.Resources>
                  <Border x:Name="Flaeche" Background="{DynamicResource FotoAkzentBrush}" Width="20" Height="20"/>
                </Border>
                """);
            Lege(fotoHost);
            var fotoFlaeche = (Border)fotoHost.FindName("Flaeche");
            Assert.Equal((Color)app.Resources["ColorAccent"], Assert.IsType<SolidColorBrush>(fotoFlaeche.Background).Color);

            // ── Laufzeitmechanismus 2 (Befund F): AlternationIndex ueber TemplatedParent ─────
            var liste = (ItemsControl)XamlReader.Parse("""
                <ItemsControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                              xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" AlternationCount="2">
                  <ItemsControl.ItemTemplate>
                    <DataTemplate>
                      <Border x:Name="Zeile" Height="10">
                        <Border.Style>
                          <Style TargetType="{x:Type Border}">
                            <Setter Property="Background" Value="{DynamicResource CardBrush}"/>
                            <Style.Triggers>
                              <DataTrigger Binding="{Binding (ItemsControl.AlternationIndex), RelativeSource={RelativeSource TemplatedParent}}" Value="1">
                                <Setter Property="Background" Value="{DynamicResource SurfaceSubtleBrush}"/>
                              </DataTrigger>
                            </Style.Triggers>
                          </Style>
                        </Border.Style>
                      </Border>
                    </DataTemplate>
                  </ItemsControl.ItemTemplate>
                </ItemsControl>
                """);
            liste.ItemsSource = new[] { "a", "b" };
            Lege(liste);
            var zeilen = Nachfahren<Border>(liste).Where(b => b.Name == "Zeile").ToList();
            Assert.Equal(2, zeilen.Count);
            Assert.Equal(Farbe("CardBrush"), Assert.IsType<SolidColorBrush>(zeilen[0].Background).Color);
            Assert.Equal(Farbe("SurfaceSubtleBrush"), Assert.IsType<SolidColorBrush>(zeilen[1].Background).Color);

            // ── Hochkontrast: jede gefuellte Flaeche ist Highlight, ihre Schrift HighlightText ─
            ThemeManager.SetHighContrastOverlay(app.Resources, enabled: true);
            fenster.UpdateLayout();
            foreach (var knopf in new[] { primary, success, toolbar })
                Druecke(knopf, false);
            primary.UpdateLayout();
            success.UpdateLayout();
            toolbar.UpdateLayout();

            Assert.Equal(SystemColors.HighlightColor, Flaeche(primary, "MainBorder"));
            Assert.Equal(SystemColors.HighlightTextColor, TextFarbe(primaryText));
            Assert.Equal(SystemColors.HighlightColor, Flaeche(success, "MainBorder"));
            Assert.Equal(SystemColors.HighlightTextColor, TextFarbe(successText));
            Assert.Equal(SystemColors.HighlightColor, Assert.IsType<SolidColorBrush>(Teil<Border>(toolbar, "bd").Background).Color);
            Assert.Equal(SystemColors.HighlightTextColor, TextFarbe(toolbarText));
            Druecke(success, true);
            Assert.Equal(SystemColors.HighlightColor, Flaeche(success, "MainBorder"));

            item.UpdateLayout();
            Assert.Equal(SystemColors.HighlightColor, Assert.IsType<SolidColorBrush>(Teil<Border>(item, "HcAuswahlRahmen").BorderBrush).Color);
            Assert.Equal(new Thickness(0), Teil<Border>(item, "ItemBorder").BorderThickness);

            chip.UpdateLayout();
            Assert.Equal(SystemColors.HighlightTextColor, TextFarbe(chipText));

            // Der Foto-Akzent bleibt der Theme-Akzent (feste Farbe des Messfensters).
            fotoHost.UpdateLayout();
            Assert.Equal((Color)app.Resources["ColorAccent"], Assert.IsType<SolidColorBrush>(fotoFlaeche.Background).Color);

            fenster.Close();
            WpfIsolatedTestProcess.MarkChildScenarioCompleted();

            (Button Knopf, TextBlock Text) Knopf(string stil)
            {
                var text = new TextBlock { Text = "Beschriftung" };
                var b = new Button { Style = (Style)app.Resources[stil], Content = new StackPanel { Children = { text } } };
                Lege(b);
                return (b, text);
            }
        });
    }

    private static void Druecke(ButtonBase knopf, bool gedrueckt)
    {
        // IsPressed hat einen geschuetzten Setter - genau der Weg, den ButtonBase selbst geht.
        typeof(ButtonBase).GetProperty(nameof(ButtonBase.IsPressed))!.GetSetMethod(nonPublic: true)!.Invoke(knopf, new object[] { gedrueckt });
        knopf.UpdateLayout();
    }

    private static T Teil<T>(Control control, string name) where T : FrameworkElement
    {
        control.ApplyTemplate();
        return Assert.IsType<T>(control.Template.FindName(name, control));
    }

    private static Color Flaeche(Control control, string teil)
        => Assert.IsType<SolidColorBrush>(Teil<Border>(control, teil).Background).Color;

    private static Color TextFarbe(TextBlock text) => Assert.IsType<SolidColorBrush>(text.Foreground).Color;

    private static IEnumerable<T> Nachfahren<T>(DependencyObject wurzel) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(wurzel); i++)
        {
            var kind = VisualTreeHelper.GetChild(wurzel, i);
            if (kind is T t)
                yield return t;
            foreach (var n in Nachfahren<T>(kind))
                yield return n;
        }
    }
}
