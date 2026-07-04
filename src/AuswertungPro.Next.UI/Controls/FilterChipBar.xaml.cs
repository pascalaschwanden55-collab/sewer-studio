using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Filter-Chip-Leiste fuers Haltungen-Grid: Zustandsklasse (einer zur Zeit),
/// "mit Video", "mit Schaeden". Meldet jeden Wechsel als DataPageFilter —
/// die Anwendung des Filters (ICollectionView) macht der Aufrufer.
/// </summary>
public partial class FilterChipBar : UserControl
{
    /// <summary>Wird bei jeder Chip-Aenderung mit dem neuen Filterzustand ausgeloest.</summary>
    public event Action<DataPageFilter>? FilterGeaendert;

    private bool _sync;

    public FilterChipBar()
    {
        InitializeComponent();
        ApplyZustandsklasseColors();
    }

    private ToggleButton[] ZkChips => new[] { Zk0, Zk1, Zk2, Zk3, Zk4 };

    private void ApplyZustandsklasseColors()
    {
        foreach (var chip in ZkChips)
        {
            var key = chip.Content?.ToString();
            var background = ZustandsklasseColorPalette.TryGetBackground(key);
            if (background is null)
                continue;

            chip.Background = background;
            chip.Foreground = Brushes.Black;
        }
    }

    /// <summary>Zustandsklassen-Chips schliessen sich gegenseitig aus.</summary>
    private void ZkChip_Changed(object sender, RoutedEventArgs e)
    {
        if (_sync)
            return;

        if (sender is ToggleButton { IsChecked: true } aktiv)
        {
            _sync = true;
            foreach (var chip in ZkChips)
            {
                if (!ReferenceEquals(chip, aktiv))
                    chip.IsChecked = false;
            }
            _sync = false;
        }

        MeldeFilter();
    }

    private void Chip_Changed(object sender, RoutedEventArgs e)
    {
        if (!_sync)
            MeldeFilter();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _sync = true;
        foreach (var chip in ZkChips)
            chip.IsChecked = false;
        ChipVideo.IsChecked = false;
        ChipSchaeden.IsChecked = false;
        _sync = false;

        MeldeFilter();
    }

    /// <summary>Trefferanzeige aktualisieren ("X von Y Haltungen").</summary>
    public void SetTrefferInfo(int sichtbar, int gesamt)
    {
        TrefferText.Text = sichtbar == gesamt
            ? $"{gesamt} Haltungen"
            : $"{sichtbar} von {gesamt} Haltungen";
    }

    private void MeldeFilter()
    {
        var filter = AktuellerFilter();
        ResetButton.Visibility = filter.IstAktiv ? Visibility.Visible : Visibility.Collapsed;
        SperrHinweis.Visibility = filter.IstAktiv ? Visibility.Visible : Visibility.Collapsed;
        FilterGeaendert?.Invoke(filter);
    }

    private DataPageFilter AktuellerFilter()
    {
        string? zk = null;
        foreach (var chip in ZkChips)
        {
            if (chip.IsChecked == true)
            {
                zk = (string)chip.Content;
                break;
            }
        }

        return new DataPageFilter(zk, ChipVideo.IsChecked == true, ChipSchaeden.IsChecked == true);
    }
}
