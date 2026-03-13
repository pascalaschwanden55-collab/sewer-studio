using System.Windows;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class ImportPreviewWindow : Window
{
    public ImportPreviewWindow(ImportPreviewResult preview, string label)
    {
        InitializeComponent();
        WindowStateManager.Track(this);

        HeaderText.Text = $"Vorschau: {preview.RecordsToCreate} neue, " +
                          $"{preview.RecordsToUpdate} Updates, " +
                          $"{preview.ConflictsExpected} Konflikte";

        SubHeaderText.Text = $"Import-Typ: {label}";
        if (preview.MediaFilesFound > 0 || preview.MediaUnmatched > 0)
            SubHeaderText.Text += $" | Medien: {preview.MediaFilesFound} gefunden, {preview.MediaUnmatched} nicht zugeordnet";

        ChangesGrid.ItemsSource = preview.Changes;
        ConflictsGrid.ItemsSource = preview.ConflictDetails;

        if (preview.ConflictDetails.Count == 0)
            ConflictsExpander.Visibility = Visibility.Collapsed;
        else
            ConflictsExpander.IsExpanded = true;
    }

    private void ExecuteButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
