using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.UI.Behaviors;
using AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

namespace AuswertungPro.Next.UI.Tests;

public sealed class HaltungsgrafikFotoVorschauTests
{
    [Fact]
    public void Hover_zeigt_relative_Fotos_blaettert_und_schliesst_beim_Verlassen_und_Entladen()
    {
        StaTestRunner.Run(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "SewerFotoKarte-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var element = new Border { Width = 100, Height = 40 };
            var window = new Window { Width = 300, Height = 200, Left = -10000, Top = -10000,
                ShowActivated = false, ShowInTaskbar = false };
            try
            {
                SchreibeBild(Path.Combine(root, "eins.png"), Colors.Red);
                SchreibeBild(Path.Combine(root, "zwei.png"), Colors.Blue);
                var host = new Grid();
                host.Children.Add(element);
                window.Content = host;
                window.Show();
                Pumpe(100);
                PhotoHoverPreviewBehavior.SetProjectRootProvider(host, () => root);
                HaltungsgrafikFotoAktion.Verbinde(element,
                    new HaltungsgrafikMarke(0, 0, 10, 10, "Riss bei 2 m")
                    { FotoPaths = ["eins.png", "zwei.png"] }, _ => Assert.Fail("Kein externes Fenster erlaubt."));

                element.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseMoveEvent });
                Pumpe(100);
                Assert.Empty(Vorschaubilder());
                Pumpe(450);
                Assert.EndsWith("eins.png", Assert.Single(Vorschaubilder()).UriSource.LocalPath);

                var rad = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120)
                    { RoutedEvent = UIElement.PreviewMouseWheelEvent };
                element.RaiseEvent(rad);
                Assert.True(rad.Handled);
                Assert.EndsWith("zwei.png", Assert.Single(Vorschaubilder()).UriSource.LocalPath);

                element.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseLeaveEvent });
                Pumpe(250);
                Assert.Empty(Vorschaubilder());
                Klicke(element);
                Assert.Single(Vorschaubilder());
                Taste(element, Key.Escape);
                Assert.Empty(Vorschaubilder());
                Taste(element, Key.Enter);
                Assert.Single(Vorschaubilder());
                element.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                Assert.Empty(Vorschaubilder());
                // Dieselbe Grafik funktioniert nach einem Seitenwechsel erneut.
                Klicke(element);
                Assert.Single(Vorschaubilder());
                // Fehlende/defekte Bilder duerfen kein vorheriges Foto stehen lassen.
                File.WriteAllText(Path.Combine(root, "eins.png"), "kein Bild");
                Klicke(element);
                Assert.Empty(Vorschaubilder());
                Assert.Contains("nicht geladen", element.ToolTip?.ToString());
                File.Delete(Path.Combine(root, "eins.png"));
                File.Delete(Path.Combine(root, "zwei.png"));
                Klicke(element);
                Assert.Contains("nicht gefunden", element.ToolTip?.ToString());
            }
            finally
            {
                PhotoHoverPreviewBehavior.SetIsEnabled(element, false);
                window.Close();
                Directory.Delete(root, true);
            }
        });
    }

    private static void Klicke(UIElement element) => element.RaiseEvent(
        new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
        { RoutedEvent = UIElement.MouseLeftButtonUpEvent });

    private static void Taste(UIElement element, Key key) => element.RaiseEvent(
        new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(element), 0, key)
        { RoutedEvent = UIElement.KeyDownEvent });

    private static BitmapImage[] Vorschaubilder() => PresentationSource.CurrentSources
        .Cast<PresentationSource>().Where(s => s.Dispatcher == Dispatcher.CurrentDispatcher)
        .SelectMany(s => Bilder(s.RootVisual)).Select(i => i.Source).OfType<BitmapImage>().ToArray();

    private static IEnumerable<Image> Bilder(DependencyObject? element)
    {
        if (element is null) yield break;
        if (element is Image image) yield return image;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            foreach (var bild in Bilder(VisualTreeHelper.GetChild(element, i))) yield return bild;
    }

    private static void SchreibeBild(string path, Color color)
    {
        var bitmap = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Bgra32, null,
            new byte[] { color.B, color.G, color.R, 255, color.B, color.G, color.R, 255 }, 8);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void Pumpe(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
}
