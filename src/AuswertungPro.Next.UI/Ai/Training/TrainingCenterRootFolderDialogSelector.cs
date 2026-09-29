using Microsoft.Win32;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingCenterRootFolderDialogSelector
{
    public static IReadOnlyList<string> SelectFolders()
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Trainings-Ordner wählen (Mehrfachauswahl möglich)",
            Multiselect = true
        };

        return dlg.ShowDialog() == true
            ? dlg.FolderNames.ToList()
            : Array.Empty<string>();
    }
}
