using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.UseCases.PhotoAnnotations;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Services;

public static class VsaCodeExplorerDialogServiceFactory
{
    public static VsaCodeExplorerDialogService Create(ICodeUsageTracker? codeUsage = null)
        => Create(codeUsage, services: null);

    public static VsaCodeExplorerDialogService Create(
        ICodeUsageTracker? codeUsage,
        ServiceProvider? services)
        => new(request =>
        {
            IAnnotationWorkbenchService? workbench = null;
            IPhotoAnnotationUseCase? photoAnnotations = null;
            if (request.ViewModel.PhotoAnnotationContext is not null)
            {
                try
                {
                    workbench = TrainingStudioWindowDependencyFactory.Create(services);
                    photoAnnotations = new PhotoAnnotationUseCase(workbench);
                }
                catch (Exception ex)
                {
                    DialogHost.Current.Warn(
                        "Die Foto-Segmentierung konnte nicht vorbereitet werden. "
                        + "Die normale Codierung bleibt verfuegbar.\n\n"
                        + UserError.DescribeAndReport(ex, "Foto-Segmentierung vorbereiten"),
                        "KI-Segmentierung");
                }
            }

            try
            {
                var dialog = new VsaCodeExplorerWindow(
                    request.ViewModel,
                    request.VideoPath,
                    request.CurrentVideoTime,
                    codeUsage,
                    photoAnnotations)
                {
                    Owner = request.Owner,
                    LiveSnapshotProvider = request.LiveSnapshotProvider
                };

                var accepted = dialog.ShowDialog() == true && dialog.SelectedEntry is not null;
                return new VsaCodeExplorerDialogResult(
                    accepted,
                    accepted ? dialog.SelectedEntry : null);
            }
            finally
            {
                (workbench as IDisposable)?.Dispose();
            }
        });
}
