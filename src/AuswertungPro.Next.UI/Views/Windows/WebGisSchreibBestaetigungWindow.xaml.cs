using System.Collections.Generic;
using System.Linq;
using System.Windows;
using AuswertungPro.Next.Application.WebGis;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Zeigt vor dem Schreiben genau die Änderungen «WebGIS vorher → nachher» (Wunsch Pascal 28.09.2026: «beim Schreiben
/// genau sehen, was geändert wird»). Geschrieben wird nur nach «Ja». Nach dem Schreiben zeigt dasselbe Fenster je Feld,
/// was der Server bestätigt hat.
/// </summary>
public partial class WebGisSchreibBestaetigungWindow : Window
{
    private WebGisSchreibBestaetigungWindow(IReadOnlyList<WebGisSchreibZeile> zeilen)
    {
        InitializeComponent();
        Liste.ItemsSource = zeilen;
    }

    /// <summary>Frage vor dem Schreiben. True nur bei «Ja, diese Änderungen schreiben».</summary>
    public static bool Frage(Window? besitzer, IReadOnlyList<WebGisSchreibZeile> zeilen, string was)
    {
        var objekte = zeilen.Select(z => z.Objekt).Distinct().Count();
        var fenster = new WebGisSchreibBestaetigungWindow(zeilen);
        fenster.TitelText.Text = was + " ins WebGIS schreiben?";
        fenster.ErklaerText.Text = "Genau diese " + zeilen.Count + (zeilen.Count == 1 ? " Änderung" : " Änderungen")
            + (objekte > 1 ? " an " + objekte + " Objekten" : string.Empty)
            + " werden geschrieben. Alles andere bleibt, wie es ist.";
        fenster.HinweisText.Text = "Das WebGIS wurde eben nochmals gelesen. Nach dem Schreiben wird jeder Wert zurückgelesen und hier bestätigt.";
        if (besitzer is { IsLoaded: true }) fenster.Owner = besitzer;
        return fenster.ShowDialog() == true;
    }

    /// <summary>Ergebnis nach dem Schreiben: je Feld bestätigt, offen oder nicht geschrieben.</summary>
    public static void ZeigeErgebnis(Window? besitzer, IReadOnlyList<WebGisSchreibZeile> zeilen, string meldung)
    {
        var fenster = new WebGisSchreibBestaetigungWindow(zeilen);
        var bestaetigt = zeilen.Count(z => z.Ton == WebGisAnzeigeTon.Bestaetigt);
        fenster.TitelText.Text = bestaetigt == zeilen.Count
            ? "Geschrieben — alles vom WebGIS bestätigt"
            : "Geschrieben — " + bestaetigt + " von " + zeilen.Count + " bestätigt";
        fenster.ErklaerText.Text = meldung;
        fenster.HinweisText.Text = "Bericht und Protokoll liegen im Projektordner unter __WebGIS_Export.";
        fenster.SchreibenKnopf.Visibility = Visibility.Collapsed;
        fenster.AbbrechenKnopf.Content = "Schliessen";
        fenster.Title = "SewerStudio — Ergebnis im WebGIS";
        if (besitzer is { IsLoaded: true }) fenster.Owner = besitzer;
        fenster.ShowDialog();
    }

    private void OnSchreiben(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnAbbrechen(object sender, RoutedEventArgs e) => DialogResult = false;
}
