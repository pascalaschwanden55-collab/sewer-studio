using Microsoft.Win32;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingYoloExportTargetFolderSelector
{
    public static string? SelectFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "YOLO-Export Zielordner waehlen"
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
