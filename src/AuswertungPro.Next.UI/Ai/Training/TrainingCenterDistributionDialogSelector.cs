using Microsoft.Win32;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingCenterDistributionDialogSelector
{
    public static string? SelectPdfPath()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Haltungs-PDF waehlen",
            Filter = "PDF (*.pdf)|*.pdf"
        };

        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public static string? SelectVideoFolder()
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Video-Ordner waehlen (Film-Ordner mit Haltungs-Videos)"
        };

        return dlg.ShowDialog() == true ? dlg.FolderName : null;
    }
}
