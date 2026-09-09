using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.UI.Controls;
using AuswertungPro.Next.UI.Views.Pages;

namespace AuswertungPro.Next.UI.Tests;

[Collection("IsolatedWpf")]
public sealed class ImportFortschrittIsolatedSmokeTests
{
    [Fact]
    public async Task Karte_zeigt_Fortschritt_in_beiden_Themes()
    {
        var result = await WpfIsolatedTestProcess.RunAsync(
            typeof(ImportFortschrittIsolatedSmokeTests).FullName + "." + nameof(Kindprozess_Importkarte),
            TimeSpan.FromSeconds(60));
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess_Importkarte()
    {
        StaTestRunner.Run(() =>
        {
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();
            foreach (var theme in new[] { "ThemeLight", "Theme" })
            {
                app.Resources.MergedDictionaries[0] = new ResourceDictionary
                {
                    Source = new Uri($"/SewerStudio;component/Theme/{theme}.xaml", UriKind.Relative)
                };
                var page = new ImportPage
                {
                    DataContext = new
                    {
                        IsImportInProgress = true, CanCancel = true,
                        ImportPhase = "Schritt 6 von 7 · Schachtprotokolle",
                        ImportProgress = "Datei: Schachtprotokoll_22152.pdf",
                        ImportCounter = "39 von 261", ImportProgressPercent = 15.0,
                        ImportIsIndeterminate = false,
                        ImportRemaining = "Schachtprotokolle: noch ca. 12 Minuten"
                    }
                };
                page.Measure(new Size(850, 800));
                page.Arrange(new Rect(0, 0, 850, 800));
                page.UpdateLayout();
                var bar = Find<ProgressBar>(page)!;
                Assert.NotNull(bar);
                Assert.Equal(10, bar.ActualHeight);
                Assert.False(bar.IsIndeterminate);
                Assert.Equal(15, bar.Value);
                var indicator = (Border)bar.Template.FindName("PART_Indicator", bar);
                var track = (Border)bar.Template.FindName("PART_Track", bar);
                Assert.Equal(new CornerRadius(5), indicator.CornerRadius);
                Assert.InRange(indicator.ActualWidth / track.ActualWidth, 0.149, 0.151);
                Assert.True(Find<NeuralPulseDot>(page)!.IsActive);

                if (Environment.GetEnvironmentVariable("SEWER_IMPORT_PROGRESS_SCREENSHOTS") is { Length: > 0 } folder)
                {
                    var card = (Border)((StackPanel)bar.Parent).Parent;
                    var bitmap = new RenderTargetBitmap((int)card.ActualWidth, (int)card.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    var visual = new DrawingVisual();
                    using (var drawing = visual.RenderOpen())
                        drawing.DrawRectangle(new VisualBrush(card), null, new Rect(0, 0, card.ActualWidth, card.ActualHeight));
                    bitmap.Render(visual);
                    Directory.CreateDirectory(folder);
                    using var file = File.Create(Path.Combine(folder, theme + ".png"));
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    encoder.Save(file);
                }
                bar.IsIndeterminate = true;
                page.UpdateLayout();
                Assert.InRange(indicator.ActualWidth / track.ActualWidth, 0.999, 1.001);
            }
            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
        });
    }

    private static T? Find<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) return match;
            if (Find<T>(child) is { } nested) return nested;
        }
        return null;
    }
}
