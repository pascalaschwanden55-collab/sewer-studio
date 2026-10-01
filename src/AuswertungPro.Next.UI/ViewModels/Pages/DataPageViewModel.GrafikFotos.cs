using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class DataPageViewModel
{
    internal string? GrafikFotoProjektRoot =>
        AuswertungPro.Next.Application.Common.ProjectFileLocator.ProjectRootFromFile(_settings.LastProjectPath);

    internal void OeffneGrafikFoto(IReadOnlyList<string> paths)
    {
        var result = new BeobachtungenPhotoOpenController(_inspectionProtocolFiles, _shellOpen)
            .OpenFirst(paths, _settings.LastProjectPath);
        if (result.Status == BeobachtungenPhotoOpenStatus.NotFound)
            _dialogs.Warn("Das hinterlegte Foto wurde nicht gefunden.", "Ereignisfoto");
        else if (result.Status == BeobachtungenPhotoOpenStatus.OpenFailed)
            _dialogs.Warn($"Das Foto konnte nicht geöffnet werden.\n{result.Error}", "Ereignisfoto");
    }
}
