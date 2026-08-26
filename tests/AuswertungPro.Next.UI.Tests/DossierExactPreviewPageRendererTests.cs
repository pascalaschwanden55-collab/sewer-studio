using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.UI.Views.Rendering;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DossierExactPreviewPageRendererTests
{
    [Fact]
    public void Render_zeigt_das_PDF_Bild_im_echten_Seitenverhaeltnis_und_teilt_Klickflaechen()
    {
        RunOnSta(() =>
        {
            var bitmap = new WriteableBitmap(100, 150, 96, 96, PixelFormats.Bgra32, null);
            var word = new DossierOutputPreviewWord("Test", 72, 72, 144, 90);
            var page = new DossierOutputPreviewPage(1, 612, 792, "Test", [word]);
            var field = DossierPreviewTarget.Field("Testfeld");
            var literal = DossierPreviewTarget.Literal("Test");

            var result = DossierExactPreviewPageRenderer.Render(
                bitmap,
                page,
                new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>
                {
                    [0] = [field, literal]
                });

            Assert.Equal(816, result.Root.Width, 3);
            Assert.Equal(1056, result.Root.Height, 3);
            var fieldFrame = Assert.Single(result.Frames[field]);
            Assert.Same(fieldFrame, Assert.Single(result.Frames[literal]));
            Assert.True(fieldFrame.Width > 90);
            Assert.True(fieldFrame.Height > 20);
        });
    }

    [Fact]
    public void Render_zeigt_auf_der_Planseite_einen_sichtbaren_Fotoknopf()
    {
        RunOnSta(() =>
        {
            var bitmap = new WriteableBitmap(100, 150, 96, 96, PixelFormats.Bgra32, null);
            var heading = new DossierOutputPreviewWord(
                "Übersichtsplan", 72, 700, 160, 718);
            var page = new DossierOutputPreviewPage(
                3, 612, 792, "Übersichtsplan Werkleitungen", [heading]);
            var plan = DossierPreviewTarget.Field("Uebersichtsplan");

            var result = DossierExactPreviewPageRenderer.Render(
                bitmap,
                page,
                new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>(),
                plan);

            var button = Assert.Single(result.Frames[plan]);
            Assert.Equal(190, button.Width);
            Assert.Equal(Cursors.Hand, button.Cursor);
            Assert.Contains("Werkleitungsplan", button.ToolTip?.ToString());
            Assert.Single(Assert.IsType<Canvas>(result.Overlay).Children);
        });
    }

    [Fact]
    public void Render_zeigt_auf_einer_anderen_Seite_keinen_Fotoknopf()
    {
        RunOnSta(() =>
        {
            var bitmap = new WriteableBitmap(100, 150, 96, 96, PixelFormats.Bgra32, null);
            var page = new DossierOutputPreviewPage(4, 612, 792, "Eigentümer", []);

            var result = DossierExactPreviewPageRenderer.Render(
                bitmap,
                page,
                new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>());

            Assert.Empty(result.Frames);
            Assert.Empty(Assert.IsType<Canvas>(result.Overlay).Children);
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null)
            throw new Xunit.Sdk.XunitException(error.ToString());
    }
}
