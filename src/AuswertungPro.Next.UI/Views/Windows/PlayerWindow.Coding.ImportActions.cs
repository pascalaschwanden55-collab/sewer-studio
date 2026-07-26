using System;
using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Helpers;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Rechte (Import-)Spalte des Abgleich-Panels: dieselben Aktionen wie links —
/// Fotos anzeigen, Bearbeiten (Code / BBox ziehen) und Bestaetigen → ins KI-Brain.
/// Alle nutzen die bestehenden Workflows, nur auf <c>LstImportEvents.SelectedItem</c> bezogen.
/// </summary>
public partial class PlayerWindow
{
    private void ImportShowPhotos_Click(object sender, RoutedEventArgs e)
    {
        CodingPhotoViewerCommandWorkflow.Execute(
            new CodingPhotoViewerCommandRequest(LstImportEvents.SelectedItem),
            new CodingPhotoViewerCommandActions(
                ShowNoPhotosOverlay: () => ShowOverlay(
                    "Keine Fotos vorhanden. 'Bearbeiten' zum Erfassen.",
                    TimeSpan.FromSeconds(3)),
                ShowViewer: codingEvent => CodingPhotoViewerDisplayWorkflow.Show(
                    this, codingEvent, _protocolContext.LastProjectPath,
                    _protocolContext.CodingDefectPreviews)));
    }

    private void ImportEdit_Click(object sender, RoutedEventArgs e)
    {
        // Bearbeiten oeffnet den modernen VSA-Explorer; dort ist der PhotoAssistant mit
        // dem Rechteck/MarkRect-Werkzeug (BBox ziehen). Danach Abgleich neu rechnen.
        if (LstImportEvents.SelectedItem is CodingEvent ev && TryEditCodingEvent(ev))
            _codingProtocolMatchController.RunMatch();
    }

    private void ImportConfirmToBrain_Click(object sender, RoutedEventArgs e)
        => HandleImportConfirmToBrainAsync().SafeFireAndForget("ImportConfirmToBrain");

    private async Task HandleImportConfirmToBrainAsync()
    {
        await _codingImportReferenceConfirmationController.ExecuteAsync(
            LstImportEvents.SelectedItem as CodingEvent,
            new CodingImportReferenceConfirmationActions(
                ShowMissingCode: () => ShowOverlay(
                    "Kein VSA-Code — bitte zuerst 'Bearbeiten'.",
                    TimeSpan.FromSeconds(3)),
                PersistTrainingSampleAsync: _codingTrainingPersistenceContext.PersistSingleEventAsync,
                ShowSuccess: () => ShowOverlay(
                    "Ins KI-Brain uebernommen.",
                    TimeSpan.FromSeconds(2)),
                RefreshProtocolMatch: () => _codingProtocolMatchController.RunMatch(),
                PersistTrainingSampleWithResultAsync:
                    _codingTrainingPersistenceContext.PersistSingleEventAsync,
                ShowPersistenceError: error => ShowOverlay(
                    $"Nicht ins KI-Brain uebernommen: {error}",
                    TimeSpan.FromSeconds(5))));
    }
}
