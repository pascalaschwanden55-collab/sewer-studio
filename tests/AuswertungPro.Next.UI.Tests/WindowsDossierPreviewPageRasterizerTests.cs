using System.Threading;

using AuswertungPro.Next.UI.Services;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AuswertungPro.Next.UI.Tests;

public sealed class WindowsDossierPreviewPageRasterizerTests
{
    [Fact]
    public void RenderAsync_zeichnet_eine_vollstaendige_A4_PDF_Seite()
    {
        RunOnSta(() =>
        {
            QuestPDF.Settings.License = LicenseType.Community;
            var pdf = Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.Content().Text("Exakte Dossierseite").FontFamily("Arial");
                });
            }).GeneratePdf();

            var bitmap = new WindowsDossierPreviewPageRasterizer()
                .RenderAsync(pdf, pageIndex: 0, destinationWidth: 900)
                .GetAwaiter()
                .GetResult();

            Assert.Equal(900, bitmap.PixelWidth);
            Assert.InRange(bitmap.PixelHeight, 1270, 1275);
            Assert.True(bitmap.IsFrozen);
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
