using System;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class FloatingGridWindow : Window
{
    /// <summary>Wird ausgeloest wenn der Benutzer "Andocken" klickt.</summary>
    public event Action? DockBackRequested;

    public FloatingGridWindow()
    {
        InitializeComponent();
        WindowStateManager.Track(this);
    }

    /// <summary>Setzt das DataGrid als Inhalt des Fensters.</summary>
    public void SetGridContent(UIElement grid)
    {
        GridHost.Child = grid;
    }

    /// <summary>Entfernt das DataGrid aus dem Fenster und gibt es zurueck.</summary>
    public UIElement? RemoveGridContent()
    {
        var child = GridHost.Child;
        GridHost.Child = null;
        return child;
    }

    /// <summary>Aktualisiert Titel und Statusleiste.</summary>
    public void UpdateInfo(string? projectName, int recordCount, string? selectedHaltung)
    {
        TitleText.Text = string.IsNullOrWhiteSpace(projectName)
            ? "Haltungen (abgedockt)"
            : $"Haltungen - {projectName} (abgedockt)";
        Title = TitleText.Text;

        RecordCountText.Text = $"{recordCount} Haltungen";
        SelectedInfoText.Text = string.IsNullOrWhiteSpace(selectedHaltung)
            ? ""
            : $"Ausgewaehlt: {selectedHaltung}";
    }

    /// <summary>Stellt die Fensterposition aus gespeicherten Bounds wieder her.</summary>
    public void ApplySavedBounds(string? boundsString)
    {
        if (string.IsNullOrWhiteSpace(boundsString))
            return;

        var parts = boundsString.Split(',');
        if (parts.Length != 4)
            return;

        if (double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) &&
            double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y) &&
            double.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w) &&
            double.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var h))
        {
            if (w > 100 && h > 100)
            {
                Left = x;
                Top = y;
                Width = w;
                Height = h;
                WindowStartupLocation = WindowStartupLocation.Manual;
            }
        }
    }

    /// <summary>Gibt die aktuelle Fensterposition als String zurueck.</summary>
    public string GetBoundsString()
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        return $"{Left.ToString(inv)},{Top.ToString(inv)},{Width.ToString(inv)},{Height.ToString(inv)}";
    }

    private void DockBack_Click(object sender, RoutedEventArgs e)
    {
        DockBackRequested?.Invoke();
    }
}
