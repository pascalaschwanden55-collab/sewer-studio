using System.Windows;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Grossansicht eines Bogen-Vorschlags: Spitzenbild in Fenstergroesse, Clip darunter.
/// Reine Anzeige — die Daumen im Bereich taugen nicht zur Beurteilung
/// (Sichtpruefung 2026-08-09: "ich erkenne nichts").
/// </summary>
public partial class BendSuggestionPreviewWindow : Window
{
    public BendSuggestionPreviewWindow()
    {
        InitializeComponent();
    }

    /// <summary>Setzt Titel, Bild und optionalen Clip. Ohne Clip bleibt die Flaeche leer.</summary>
    public void SetContent(string ortText, ImageSource? spitzenbild, string? clipPath)
    {
        TitelText.Text = $"Bogen-Vorschlag · {ortText}";
        GrossBild.Source = spitzenbild;
        if (!string.IsNullOrWhiteSpace(clipPath))
        {
            ClipAnzeige.Source = new System.Uri(clipPath);
        }
        else
        {
            ClipAnzeige.Visibility = Visibility.Collapsed;
        }
    }
}
