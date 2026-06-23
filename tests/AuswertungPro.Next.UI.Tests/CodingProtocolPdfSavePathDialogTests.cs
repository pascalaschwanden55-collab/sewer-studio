using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolPdfSavePathDialogTests
{
    [Fact]
    public void Show_returns_selected_path_from_dialog_delegate()
    {
        var dialog = new CodingProtocolPdfSavePathDialog(defaultFileName => $@"C:\out\{defaultFileName}");

        var result = dialog.Show("haltung.pdf");

        Assert.Equal(@"C:\out\haltung.pdf", result);
    }

    [Fact]
    public void Show_returns_null_when_dialog_is_cancelled()
    {
        var dialog = new CodingProtocolPdfSavePathDialog(_ => null);

        var result = dialog.Show("haltung.pdf");

        Assert.Null(result);
    }
}
