using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.UI.Ai.Teacher;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Views.Windows;

// Lehrer-Annotationen Galerie + FewShot-Verwaltung. Aus dem Hauptdatei
// extrahiert (Slice 9c). Felder _allTeacherAnnotations/_filteredTeacher
// Annotations/_selectedTeacherAnnotation/_teacherLoaded bleiben in der
// Hauptdatei.
public partial class TrainingCenterWindow
{
    private async void TeacherRefresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadTeacherAnnotationsAsync();
    }

    private async Task LoadTeacherAnnotationsAsync()
    {
        try
        {
            var all = await TeacherAnnotationStore.LoadAsync();

            // Bereits als FewShot uebernommene Annotationen ausfiltern
            var trainedIds = await GetTrainedAnnotationIdsAsync();
            _allTeacherAnnotations = trainedIds.Count > 0
                ? all.Where(a => !trainedIds.Contains(a.AnnotationId)).ToList()
                : all;

            // Filter-ComboBox mit vorhandenen VSA-Codes fuellen
            var codes = _allTeacherAnnotations
                .Select(a => a.VsaCode)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .ToList();

            TeacherFilterCombo.Items.Clear();
            TeacherFilterCombo.Items.Add(new ComboBoxItem { Content = "Alle", IsSelected = true });
            foreach (var code in codes)
                TeacherFilterCombo.Items.Add(new ComboBoxItem { Content = code });

            TeacherFilterCombo.SelectedIndex = 0;
            ApplyTeacherFilter();
            _teacherLoaded = true;
        }
        catch (Exception ex)
        {
            _dialogs.ShowMessage($"Fehler beim Laden der Lehrer-Annotationen:\n{ex.Message}",
                "Lehrer", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Laedt die IDs aller Lehrer-Annotationen die bereits als FewShot-Beispiel uebernommen wurden.
    /// Format im FewShot-Store: Source = "teacher:{annotationId}"
    /// </summary>
    private static async Task<HashSet<string>> GetTrainedAnnotationIdsAsync()
    {
        try
        {
            var store = new AuswertungPro.Next.Application.Ai.Training.FewShotExampleStore();
            await store.LoadAsync();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var ex in store.Examples)
            {
                if (ex.Source is not null && ex.Source.StartsWith("teacher:", StringComparison.Ordinal))
                    ids.Add(ex.Source.Substring("teacher:".Length));
            }
            return ids;
        }
        catch
        {
            return new HashSet<string>();
        }
    }

    private void TeacherFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_teacherLoaded) return;
        ApplyTeacherFilter();
    }

    private void ApplyTeacherFilter()
    {
        var selectedItem = TeacherFilterCombo.SelectedItem as ComboBoxItem;
        var filterCode = selectedItem?.Content?.ToString();

        _filteredTeacherAnnotations = (filterCode == "Alle" || string.IsNullOrEmpty(filterCode))
            ? new List<TeacherAnnotation>(_allTeacherAnnotations)
            : _allTeacherAnnotations
                .Where(a => a.VsaCode.Equals(filterCode, StringComparison.OrdinalIgnoreCase))
                .ToList();

        TeacherGallery.ItemsSource = _filteredTeacherAnnotations;
        TeacherCountText.Text = $"{_filteredTeacherAnnotations.Count} Annotationen";

        // Selection zuruecksetzen
        _selectedTeacherAnnotation = null;
        TeacherDetailPanel.Visibility = Visibility.Collapsed;
        BtnTeacherAddFewShot.IsEnabled = false;
        BtnTeacherDelete.IsEnabled = false;
    }

    private void TeacherThumb_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not TeacherAnnotation annotation)
            return;

        _selectedTeacherAnnotation = annotation;
        BtnTeacherAddFewShot.IsEnabled = true;
        BtnTeacherDelete.IsEnabled = true;

        // Detail-Ansicht fuellen
        TeacherDetailPanel.Visibility = Visibility.Visible;
        TeacherDetailCode.Text = annotation.VsaCode;
        TeacherDetailBeschreibung.Text = annotation.Beschreibung;
        TeacherDetailMeter.Text = $"Meter: {annotation.MeterPosition:F2}m";
        TeacherDetailClock.Text = annotation.ClockPosition.HasValue
            ? $"Uhr: {annotation.ClockPosition.Value:F1}"
            : "Uhr: –";
        TeacherDetailTool.Text = $"Tool: {annotation.ToolType}";
        TeacherDetailDate.Text = $"Erstellt: {annotation.CreatedUtc.LocalDateTime:yyyy-MM-dd HH:mm}";
        TeacherDetailId.Text = $"ID: {annotation.AnnotationId}";

        // Volles Frame laden
        var framePath = annotation.FullFramePath;
        if (!string.IsNullOrEmpty(framePath) && File.Exists(framePath))
        {
            try
            {
                var converter = new FileToImageConverter();
                TeacherDetailImage.Source = converter.Convert(framePath, typeof(BitmapImage), null,
                    CultureInfo.InvariantCulture) as BitmapImage;
            }
            catch { TeacherDetailImage.Source = null; }
        }
        else
        {
            TeacherDetailImage.Source = null;
        }
    }

    private async void TeacherAddToFewShot_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTeacherAnnotation is null) return;

        var imagePath = _selectedTeacherAnnotation.CroppedRegionPath;
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            imagePath = _selectedTeacherAnnotation.FullFramePath;

        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
        {
            _dialogs.ShowMessage("Kein Bild fuer diese Annotation verfuegbar.",
                "FewShot", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var store = new FewShotExampleStore();
            await store.LoadAsync();

            var imageBytes = await File.ReadAllBytesAsync(imagePath);
            var ext = System.IO.Path.GetExtension(imagePath).ToLowerInvariant();
            var clockStr = _selectedTeacherAnnotation.ClockPosition.HasValue
                ? $"{_selectedTeacherAnnotation.ClockPosition.Value:F0} Uhr"
                : null;

            await store.AddExampleAsync(
                imageBytes, ext,
                _selectedTeacherAnnotation.VsaCode,
                _selectedTeacherAnnotation.Beschreibung,
                clockStr,
                _selectedTeacherAnnotation.MeterPosition,
                null, null,
                $"teacher:{_selectedTeacherAnnotation.AnnotationId}",
                1.0);

            _dialogs.ShowMessage(
                $"Annotation '{_selectedTeacherAnnotation.VsaCode}' als FewShot-Beispiel hinzugefuegt (quality=1.0).",
                "FewShot", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _dialogs.ShowMessage($"Fehler: {ex.Message}", "FewShot", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void TeacherDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTeacherAnnotation is null) return;

        var result = _dialogs.ShowMessage(
            $"Annotation '{_selectedTeacherAnnotation.VsaCode}' bei {_selectedTeacherAnnotation.MeterPosition:F1}m wirklich loeschen?\n\n" +
            "Zugehoerige Dateien (Frame, Crop, YOLO-Label) werden ebenfalls entfernt.",
            "Annotation loeschen",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            // Dateien loeschen (best effort)
            TryDeleteFile(_selectedTeacherAnnotation.FullFramePath);
            TryDeleteFile(_selectedTeacherAnnotation.CroppedRegionPath);
            TryDeleteFile(_selectedTeacherAnnotation.YoloAnnotationPath);

            // Aus Store entfernen (neu laden, filtern, speichern)
            var all = await TeacherAnnotationStore.LoadAsync();
            var remaining = all.Where(a => a.AnnotationId != _selectedTeacherAnnotation.AnnotationId).ToList();

            // Direkt in JSON schreiben (Store hat keine Delete-Methode — Append-only umgehen)
            var storePath = System.IO.Path.Combine(Ai.KnowledgeRoot.GetRoot(), "teacher_annotations.json");
            var json = System.Text.Json.JsonSerializer.Serialize(remaining,
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                });
            await File.WriteAllTextAsync(storePath, json);

            // Galerie neu laden
            await LoadTeacherAnnotationsAsync();
        }
        catch (Exception ex)
        {
            _dialogs.ShowMessage($"Fehler beim Loeschen: {ex.Message}",
                "Lehrer", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RemoveSelectedCases_Click(object sender, RoutedEventArgs e)
    {
        // CasesGrid wird dynamisch aus dem XAML-Baum gesucht (x:Name noch nicht definiert)
        var grid = this.FindName("CasesGrid") as System.Windows.Controls.DataGrid;
        var selected = grid?.SelectedItems.Cast<TrainingCase>().ToList();
        if (selected is null or { Count: 0 }) return;
        await Vm.RemoveSelectedCasesCommand.ExecuteAsync(selected);
    }

    private async void RemoveAllCases_Click(object sender, RoutedEventArgs e)
    {
        await Vm.RemoveAllCasesCommand.ExecuteAsync(null);
    }

    private static void TryDeleteFile(string? path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                File.Delete(path);
        }
        catch { /* best effort */ }
    }
}
