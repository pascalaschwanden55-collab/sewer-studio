using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>
    /// Verdrahtet den Projekt-Root der Hover-Foto-Vorschau in Codier-/Import-Liste, damit
    /// relative Foto-Pfade der CodingEvents aufgeloest werden (absolute funktionieren ohnehin).
    /// Der Selektor selbst haengt bereits im Konstruktor des Side-Panels.
    /// </summary>
    private void WireCodingPhotoHoverPreview()
    {
        CodingSidePanelControl.SetCodingPhotoProjectRootProvider(
            () => ProjectFileLocator.ProjectRootFromFile(_protocolContext.LastProjectPath));
    }
}
