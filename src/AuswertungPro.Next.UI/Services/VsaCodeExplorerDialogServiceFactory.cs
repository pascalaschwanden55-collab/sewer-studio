using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Services;

public static class VsaCodeExplorerDialogServiceFactory
{
    public static VsaCodeExplorerDialogService Create()
        => new(request =>
        {
            var dialog = new VsaCodeExplorerWindow(
                request.ViewModel,
                request.VideoPath,
                request.CurrentVideoTime)
            {
                Owner = request.Owner,
                LiveSnapshotProvider = request.LiveSnapshotProvider
            };

            var accepted = dialog.ShowDialog() == true && dialog.SelectedEntry is not null;
            return new VsaCodeExplorerDialogResult(
                accepted,
                accepted ? dialog.SelectedEntry : null);
        });
}
