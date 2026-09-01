using System.Threading;
using System.Windows.Controls;
using System.Windows.Documents;
using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.UI.Views.Rendering;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DossierPreviewRendererConcurrencyTests
{
    [Fact]
    public void Parallele_Vorschauen_vertauschen_keine_eigenen_Texte()
    {
        var page = new DossierPreviewPage(
            1,
            "Test",
            new DossierPreviewGeometry(800, 1000, DossierPreviewEdges.All(20)),
            [
                new DossierPreviewParagraph(
                    [DossierPreviewRun.Field("Feld", DossierPreviewRunFormat.Default)],
                    DossierPreviewParagraphFormat.Default),
                new DossierPreviewParagraph(
                    [DossierPreviewRun.Literal("Original", DossierPreviewRunFormat.Default)],
                    DossierPreviewParagraphFormat.Default)
            ],
            ["Feld"]);

        using var firstReachedField = new ManualResetEventSlim();
        using var secondFinished = new ManualResetEventSlim();
        Exception? firstError = null;
        Exception? secondError = null;
        string? firstText = null;
        string? secondText = null;

        var first = NewStaThread(() =>
        {
            try
            {
                var result = DossierPreviewPageRenderer.Render(
                    page,
                    key =>
                    {
                        if (key == "Feld")
                        {
                            firstReachedField.Set();
                            Assert.True(secondFinished.Wait(TimeSpan.FromSeconds(10)));
                        }

                        return string.Empty;
                    },
                    _ => [],
                    _ => string.Empty,
                    _ => "Erste Vorschau");
                firstText = ReadLiteral(result);
            }
            catch (Exception ex)
            {
                firstError = ex;
            }
        });

        var second = NewStaThread(() =>
        {
            try
            {
                Assert.True(firstReachedField.Wait(TimeSpan.FromSeconds(10)));
                var result = DossierPreviewPageRenderer.Render(
                    page,
                    _ => string.Empty,
                    _ => [],
                    _ => string.Empty,
                    _ => "Zweite Vorschau");
                secondText = ReadLiteral(result);
            }
            catch (Exception ex)
            {
                secondError = ex;
            }
            finally
            {
                secondFinished.Set();
            }
        });

        first.Start();
        second.Start();
        Assert.True(first.Join(TimeSpan.FromSeconds(15)), "Erste Vorschau wurde nicht beendet.");
        Assert.True(second.Join(TimeSpan.FromSeconds(15)), "Zweite Vorschau wurde nicht beendet.");

        Assert.Null(firstError);
        Assert.Null(secondError);
        Assert.Equal("Erste Vorschau", firstText);
        Assert.Equal("Zweite Vorschau", secondText);
    }

    private static Thread NewStaThread(ThreadStart action)
    {
        var thread = new Thread(action);
        thread.SetApartmentState(ApartmentState.STA);
        return thread;
    }

    private static string ReadLiteral(DossierPreviewRenderResult result)
    {
        var border = Assert.Single(result.Frames[DossierPreviewTarget.Literal("Original")]);
        var text = Assert.IsType<TextBlock>(border.Child);
        return string.Concat(text.Inlines.OfType<Run>().Select(run => run.Text));
    }
}
