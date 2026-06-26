using System;
using System.Windows;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void CodingEventShowPhotos_Click(object sender, RoutedEventArgs e)
    {
        CodingPhotoViewerCommandWorkflow.Execute(
            new CodingPhotoViewerCommandRequest(LstCodingEvents.SelectedItem),
            new CodingPhotoViewerCommandActions(
                ShowNoPhotosOverlay: () => ShowOverlay(
                    "Keine Fotos vorhanden. Doppelklick zum Bearbeiten.",
                    TimeSpan.FromSeconds(3)),
                ShowViewer: codingEvent => CodingPhotoViewerDisplayWorkflow.Show(
                    this,
                    codingEvent,
                    _dependencies.LastProjectPath,
                    new CodingPhotoViewerDisplayWorkflowActions(
                        CreateService: CodingPhotoViewerWorkflowServiceFactory.Create))));
    }
}
