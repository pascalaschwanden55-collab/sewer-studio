using System;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Domain.Models;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Schadens-Laengsband fuer die Haltungsansicht (WinCan-artige Sektionsgrafik):
/// zeigt alle Beobachtungen der gewaehlten Haltung an ihrer Meterposition,
/// Streckenschaeden als Balken, Farben nach Code-Gruppe. Klick auf einen
/// Marker meldet den zugehoerigen Protokolleintrag (MarkerClicked).
/// </summary>
public partial class HaltungSchadensband : UserControl
{
    /// <summary>Wird beim Klick auf einen Marker mit dem Quell-Protokolleintrag ausgeloest.</summary>
    public event Action<object>? MarkerClicked;

    public HaltungSchadensband()
    {
        InitializeComponent();

        Timeline.MeterAccessor = o => ((SchadensbandMarker)o).Meter;
        Timeline.CodeAccessor = o => ((SchadensbandMarker)o).Code;
        Timeline.EndMeterAccessor = o => ((SchadensbandMarker)o).MeterEnd;
        Timeline.ColorKindAccessor = o => ((SchadensbandMarker)o).Farbe;
        Timeline.MarkerClickedCommand = new RelayCommand<object?>(marker =>
        {
            if (marker is SchadensbandMarker sm)
                MarkerClicked?.Invoke(sm.Quelle);
        });
    }

    /// <summary>Band fuer die Haltung neu aufbauen; ohne Beobachtungen wird es ausgeblendet.</summary>
    public void Update(HaltungRecord? record)
    {
        var daten = HaltungSchadensbandBuilder.Build(record);
        Timeline.TotalLength = daten.TotalLength;
        Timeline.Markers = daten.Marker;
        Visibility = daten.Marker.Count > 0 && daten.TotalLength > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
