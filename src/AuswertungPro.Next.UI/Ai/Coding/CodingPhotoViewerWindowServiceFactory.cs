using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingPhotoViewerWindowServiceFactory
{
    public static CodingPhotoViewerWindowService Create()
        => new CodingPhotoViewerWindowService();

    public static CodingPhotoViewerWindowService Create(
        ICodingDefectPreviewRenderer previewRenderer)
    {
        ArgumentNullException.ThrowIfNull(previewRenderer);

        return new CodingPhotoViewerWindowService(
            (codingEvent, projectFolder) => CodingPhotoViewerImageSourceLoader.Load(
                codingEvent,
                projectFolder,
                previewPathBuilder: item => previewRenderer.BuildPreviewImagePath(item)),
            Services.WindowStateManager.Track,
            window => window.Show());
    }
}
