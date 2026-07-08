using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
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
            HaltungLastProjectPath = value?.LastProjectPath;
        }
    }

    // Projektdatei-Pfad fuer die Gegeninspektions-Existenzpruefung in der Liste (⇄-Marker).
    // DependencyProperty, damit die Zeilen-Bindings bei Projektwechsel aktualisieren.
    public static readonly DependencyProperty HaltungLastProjectPathProperty =
        DependencyProperty.Register(
            nameof(HaltungLastProjectPath), typeof(string), typeof(HaltungsansichtView),
            new PropertyMetadata(null));

    public string? HaltungLastProjectPath
    {
        get => (string?)GetValue(HaltungLastProjectPathProperty);
        set => SetValue(HaltungLastProjectPathProperty, value);
    }

    // Basis-Namen der Haltungen mit Doppel-/Mehrfachinspektion (zweiter ".01"-Record).
    // DependencyProperty, damit der Marker in der Liste bei Datenwechsel aktualisiert.
    public static readonly DependencyProperty MehrfachInspektionsBasenProperty =
        DependencyProperty.Register(
            nameof(MehrfachInspektionsBasen), typeof(IReadOnlySet<string>),
            typeof(HaltungsansichtView), new PropertyMetadata(null));

    public IReadOnlySet<string>? MehrfachInspektionsBasen
    {
        get => (IReadOnlySet<string>?)GetValue(MehrfachInspektionsBasenProperty);
        set => SetValue(MehrfachInspektionsBasenProperty, value);
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

    // Jeder Linksklick auf eine Haltung meldet die Auswahl an die QGIS-Bridge —
    // auch wenn dieselbe (bereits markierte) Haltung erneut geklickt wird. Nur so
    // zoomt QGIS nach manuellem Wegschwenken erneut auf die Haltung (Auswahl-Stempel).
    // Der SelectionChanged-Pfad ueber das ViewModel feuert bei gleichbleibender
    // Auswahl NICHT, deshalb hier zusaetzlich direkt am Mausklick.
    private void HaltungList_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _ = sender;
        var dep = e.OriginalSource as System.Windows.DependencyObject;
        while (dep is not null and not System.Windows.Controls.ListBoxItem)
            dep = AuswertungPro.Next.UI.Behaviors.VisualTreeSafe.GetParentSafe(dep);
        if (dep is System.Windows.Controls.ListBoxItem { DataContext: HaltungRecord record })
        {
            var name = record.GetFieldValue("Haltungsname");
            if (!string.IsNullOrWhiteSpace(name))
                QgisBridge.QgisBridgeSelection.Set(name);
        }
    }

    // Rechtsklick wählt zuerst die Zeile unter dem Cursor, damit das Menü auf der
    // richtigen Haltung arbeitet (auch wenn sie nicht selektiert war).
    private void HaltungList_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _ = sender;
        var dep = e.OriginalSource as System.Windows.DependencyObject;
        while (dep is not null and not System.Windows.Controls.ListBoxItem)
            dep = AuswertungPro.Next.UI.Behaviors.VisualTreeSafe.GetParentSafe(dep);
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
    private void CtxPlayGegen_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("playgegen"); }
    private void CtxPrintAwu_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("printawu"); }
    private void CtxOpenPdf_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("openpdf"); }
    private void CtxOpenFolder_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("openfolder"); }
    private void CtxCosts_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("costs"); }
    private void CtxDelete_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("delete"); }

    private void RefreshDetail()
    {
        if (!IsVisible)
            return;

        // Aktive Haltung in der Liste in Sicht halten: beim (Wieder-)Anzeigen der Ansicht
        // und bei Auswahl von aussen (Karte/QGIS) an die aktive Zeile scrollen. ScrollIntoView
        // bewegt die Liste nur, wenn die Zeile nicht ohnehin sichtbar ist — kein Springen bei
        // bereits sichtbarer Auswahl.
        if (HaltungList.SelectedItem is { } activeItem)
            Dispatcher.InvokeAsync(
                () => HaltungList.ScrollIntoView(activeItem),
                System.Windows.Threading.DispatcherPriority.Background);

        // Projektpfad fuer die Gegeninspektions-Marker (⇄) in der Liste aktuell halten.
        HaltungLastProjectPath = _settings?.LastProjectPath;

        // Doppel-/Mehrfachinspektionen erkennen (Haltungen mit zweitem ".01"-Record) —
        // fuer denselben Marker in der Liste.
        MehrfachInspektionsBasen = AuswertungPro.Next.UI.DataPage.HaltungInspektionsGruppen
            .MehrfachInspektionsBasen(
                (HaltungList.ItemsSource as System.Collections.IEnumerable ?? System.Array.Empty<object>())
                .OfType<HaltungRecord>()
                .Select(r => r.GetFieldValue("Haltungsname")));

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
