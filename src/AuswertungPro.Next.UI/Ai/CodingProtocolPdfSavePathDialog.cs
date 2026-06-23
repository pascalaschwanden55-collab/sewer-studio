using System;

namespace AuswertungPro.Next.UI.Ai;

public sealed class CodingProtocolPdfSavePathDialog
{
    private readonly Func<string, string?> _showDialog;

    public CodingProtocolPdfSavePathDialog()
        : this(ShowWpfSaveDialog)
    {
    }

    public CodingProtocolPdfSavePathDialog(Func<string, string?> showDialog)
    {
        _showDialog = showDialog ?? throw new ArgumentNullException(nameof(showDialog));
    }

    public string? Show(string defaultFileName)
        => _showDialog(defaultFileName);

    private static string? ShowWpfSaveDialog(string defaultFileName)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "PDF-Protokoll speichern",
            Filter = "PDF-Dateien (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            FileName = defaultFileName
        };

        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
