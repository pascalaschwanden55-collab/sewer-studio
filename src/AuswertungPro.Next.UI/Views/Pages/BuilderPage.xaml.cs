using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Behaviors;
using AuswertungPro.Next.UI.Controls;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class BuilderPage : UserControl
{
    private const string PageViewKey = "BuilderPage";
    private const string GridKey = "Grid";
    private bool _viewOptionsSeeded;
    private readonly SavedViewsController _savedViews;

    /// <summary>Zeilenhoehe/Zoom des Grids (Dim3), gebunden aus dem Kontextmenue und an das DataGrid.</summary>
    public GridViewOptions ViewOptions { get; } = new();

    public BuilderPage()
    {
        InitializeComponent();
        // Das Grid-Kontextmenue (Zeilenhoehe/Zoom-Slider) bindet an die Ansichtsoptionen,
        // nicht an das Zeilen-ViewModel.
        RowsGrid.ContextMenu.DataContext = ViewOptions;
        ViewOptions.PropertyChanged += (_, _) =>
        {
            if (_viewOptionsSeeded)
                GridViewOptionsCore.Persist(ViewOptions, PageViewKey, GridKey);
        };

        _savedViews = new SavedViewsController(RowsGrid, PageViewKey);
        SavedViewsBox.ItemsSource = _savedViews.Names;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Erst Werte laden, DANN Persistenz scharfschalten (kein Speichern beim Seed).
        GridViewOptionsCore.Seed(ViewOptions, PageViewKey, GridKey);
        _viewOptionsSeeded = true;
        _savedViews.RefreshNames();
    }

    // Rechtsklick waehlt die Zeile unter dem Cursor, damit die Kontextmenue-Aktionen
    // (Kostenblatt / Volles Dossier) auf der richtigen Haltung arbeiten.
    private void RowsGrid_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _ = sender;
        var dep = e.OriginalSource as DependencyObject;
        while (dep is not null and not DataGridRow)
            dep = System.Windows.Media.VisualTreeHelper.GetParent(dep);
        if (dep is DataGridRow row)
            row.IsSelected = true;
    }

    private void SpaltenButton_Click(object sender, RoutedEventArgs e)
        => ColumnChooser.Show(RowsGrid);

    private void SavedViewsBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SavedViewsBox.SelectedItem is string name)
            _savedViews.Apply(name);
    }

    private void SaveViewButton_Click(object sender, RoutedEventArgs e)
    {
        var name = SavedViewsBox.Text?.Trim();
        if (string.IsNullOrEmpty(name))
            return;
        _savedViews.Save(name);
        SavedViewsBox.Text = name;
    }

    private void DeleteViewButton_Click(object sender, RoutedEventArgs e)
    {
        var name = (SavedViewsBox.SelectedItem as string) ?? SavedViewsBox.Text?.Trim();
        _savedViews.Delete(name);
        SavedViewsBox.Text = string.Empty;
    }
}
