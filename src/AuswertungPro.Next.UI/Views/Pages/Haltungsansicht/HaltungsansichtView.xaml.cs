using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

public partial class HaltungsansichtView : UserControl
{
    // Grenzen fuer die einstellbare Hoehe des "Primaere Schaeden"-Panels (px).
    private const double SchadenHeightMin = 80d;
    private const double SchadenHeightMax = 2000d;
    private AppSettings? _settings;

    public HaltungsansichtView()
    {
        InitializeComponent();
        RestoreSchadenHeight();
        IsVisibleChanged += (_, _) => RefreshDetail();

        // Schadensband: Marker-Klick selektiert den Eintrag in der Schaeden-Liste.
        Schadensband.MarkerClicked += quelle =>
        {
            SchadenList.SelectedItem = quelle;
            SchadenList.ScrollIntoView(quelle);
        };

        // Hover-Foto-Vorschau: Projekt-ROOT fuer relative FotoPaths. _settings wird erst nach dem
        // Konstruktor via Settings-Property gesetzt -> Closure liest den aktuellen Wert bei jedem Hover.
        Behaviors.PhotoHoverPreviewBehavior.SetProjectRootProvider(
            SchadenList,
            () => AuswertungPro.Next.Application.Common.ProjectFileLocator
                      .ProjectRootFromFile(_settings?.LastProjectPath));
    }

    public AppSettings? Settings
    {
        get => _settings;
        set
        {
            _settings = value;
            RestoreSchadenHeight();
        }
    }

    /// <summary>Gespeicherte Panel-Hoehe beim Start anwenden (geclampt).</summary>
    private void RestoreSchadenHeight()
    {
        var height = _settings?.HaltungsansichtSchadenHeight ?? double.NaN;
        if (double.IsNaN(height) || height <= 0)
            return;

        SchadenRowDef.Height = new GridLength(
            Math.Clamp(height, SchadenHeightMin, SchadenHeightMax),
            GridUnitType.Pixel);
    }

    /// <summary>Nach dem Ziehen des Splitters die neue Hoehe speichern.</summary>
    private void SchadenSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        _ = sender; _ = e;
        var settings = _settings;
        if (settings is null)
            return;

        var height = Math.Clamp(SchadenRowDef.ActualHeight, SchadenHeightMin, SchadenHeightMax);
        if (Math.Abs(settings.HaltungsansichtSchadenHeight - height) < 0.5d)
            return;

        settings.HaltungsansichtSchadenHeight = height;
        settings.Save();
    }

    private void SchadensbandDetach_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (HaltungList.SelectedItem is not HaltungRecord record)
            return;

        var band = new AuswertungPro.Next.UI.Controls.HaltungSchadensband
        {
            Margin = new Thickness(16)
        };
        band.Update(record);

        var name = record.GetFieldValue("Haltungsname");
        var window = new Window
        {
            Title = string.IsNullOrWhiteSpace(name) ? "Schadensband" : $"Schadensband - {name}",
            Owner = Window.GetWindow(this),
            Width = 920,
            Height = 260,
            MinWidth = 520,
            MinHeight = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = band,
            Background = TryFindResource("BgBrush") as Brush
        };

        WindowStateManager.Track(window);
        window.Show();
    }

    private Func<HaltungRecord, IReadOnlyList<RecordDetailGroup>>? _detailBuilder;

    /// <summary>
    /// Wird von der DataPage gesetzt: baut die editierbaren Detail-Gruppen für eine Haltung
    /// (nutzt den bestehenden Pfad CreateHaltungDetailItem/CommitHaltungDetailField).
    /// Beim Setzen wird das Detail sofort aktualisiert, damit die Zuweisungs-Reihenfolge
    /// (vor oder nach dem Sichtbarwerden) keine Rolle spielt.
    /// </summary>
    public Func<HaltungRecord, IReadOnlyList<RecordDetailGroup>>? DetailBuilder
    {
        get => _detailBuilder;
        set
        {
            _detailBuilder = value;
            RefreshDetail();
        }
    }

    /// <summary>
    /// Von der DataPage gesetzt: führt eine Aktion (actionKey) auf einer Haltung aus,
    /// indem sie die bestehenden DataPage-Handler/Commands aufruft.
    /// </summary>
    public Action<string, HaltungRecord>? ActionRequested { get; set; }

    private void HaltungList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        RefreshDetail();
    }

    private void SchadenList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _ = sender; _ = e;
        if (HaltungList.SelectedItem is HaltungRecord record)
            ActionRequested?.Invoke("codieren", record);
    }

    private void SchadenAdd_Click(object sender, RoutedEventArgs e)
    {
        _ = sender; _ = e;
        if (HaltungList.SelectedItem is HaltungRecord record)
            ActionRequested?.Invoke("codieren", record);
    }

    // Rechtsklick wählt zuerst die Zeile unter dem Cursor, damit das Menü auf der
    // richtigen Haltung arbeitet (auch wenn sie nicht selektiert war).
    private void HaltungList_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _ = sender;
        var dep = e.OriginalSource as System.Windows.DependencyObject;
        while (dep is not null and not System.Windows.Controls.ListBoxItem)
            dep = System.Windows.Media.VisualTreeHelper.GetParent(dep);
        if (dep is System.Windows.Controls.ListBoxItem { DataContext: HaltungRecord record })
            HaltungList.SelectedItem = record;
    }

    private void RaiseAction(string actionKey)
    {
        if (HaltungList.SelectedItem is HaltungRecord record)
            ActionRequested?.Invoke(actionKey, record);
    }

    private void CtxMoveUp_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("moveup"); }
    private void CtxMoveDown_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("movedown"); }
    private void CtxBeobachtungen_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("beobachtungen"); }
    private void CtxPlay_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("play"); }
    private void CtxPrintAwu_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("printawu"); }
    private void CtxOpenPdf_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("openpdf"); }
    private void CtxOpenFolder_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("openfolder"); }
    private void CtxCosts_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("costs"); }
    private void CtxDelete_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("delete"); }

    private void RefreshDetail()
    {
        if (!IsVisible)
            return;

        Schadensband.Update(HaltungList.SelectedItem as HaltungRecord);

        // Foto-Galerie: alle Schadensfotos der gewaehlten Haltung (gleiche Quelle wie die Hover-Vorschau).
        var projectRoot = AuswertungPro.Next.Application.Common.ProjectFileLocator
            .ProjectRootFromFile(_settings?.LastProjectPath);
        var fotos = AuswertungPro.Next.UI.Controls.HaltungFotoGalerieBuilder
            .Build(HaltungList.SelectedItem as HaltungRecord, projectRoot);
        FotoGalerie.Update(fotos);
        FotoExpanderHeader.Text = $"Fotos ({fotos.Count})";
        FotoExpander.Visibility = fotos.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        if (HaltungList.SelectedItem is not HaltungRecord record || DetailBuilder is null)
        {
            Detail.Header = "Keine Haltung gewählt";
            Detail.SubHeader = "Links eine Haltung waehlen.";
            Detail.Groups = Array.Empty<RecordDetailGroup>();
            return;
        }

        var name = record.GetFieldValue("Haltungsname");
        Detail.Header = string.IsNullOrWhiteSpace(name) ? "Haltungsdetails" : $"Haltung {name}";
        Detail.SubHeader = "Alle Felder editierbar - Aenderungen erscheinen sofort in der Tabelle.";
        Detail.Groups = DetailBuilder(record);
    }
}
