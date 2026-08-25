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
