namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingCenterRootFolderWorkflow
{
    public static void ApplySelected(
        IList<string> rootFolders,
        IEnumerable<string> selectedFolders,
        Action updateRootFolderDisplay)
    {
        ArgumentNullException.ThrowIfNull(rootFolders);
        ArgumentNullException.ThrowIfNull(selectedFolders);
        ArgumentNullException.ThrowIfNull(updateRootFolderDisplay);

        if (!TrainingCenterStateController.AddSelectedRootFolders(rootFolders, selectedFolders))
            return;

        updateRootFolderDisplay();
    }

    public static void Clear(
        IList<string> rootFolders,
        Action updateRootFolderDisplay)
    {
        ArgumentNullException.ThrowIfNull(rootFolders);
        ArgumentNullException.ThrowIfNull(updateRootFolderDisplay);

        TrainingCenterStateController.ReplaceRootFolders(rootFolders, Array.Empty<string>());
        updateRootFolderDisplay();
    }
}
