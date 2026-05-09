using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace AuswertungPro.Next.UI.Views.Windows;

// CodingModeWindow BBox-Result-Panel (Slice 8a.2.6): Zeigt das Ergebnis
// einer SAM/Qwen-Klassifikation als kleines Overlay-Panel oben rechts auf
// dem OverlayCanvas. Zwei Methoden + zwei dedizierte Felder. Reine
// UI-Anzeige — keine Geschaeftslogik, kein Session-State. Aus dem
// Hauptdatei extrahiert.
public partial class CodingModeWindow
{
    private Border? _bboxResultPanel;
    private TextBlock? _bboxResultText;

    private void ShowBboxResultPanel(string title, string body, bool isError)
    {
        // Bestehendes Panel entfernen
        if (_bboxResultPanel != null && OverlayCanvas.Children.Contains(_bboxResultPanel))
            OverlayCanvas.Children.Remove(_bboxResultPanel);

        _bboxResultText = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Foreground = Brushes.White,
            Text = $"{title}\n{body}",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 360
        };

        _bboxResultPanel = new Border
        {
            Background = isError
                ? new SolidColorBrush(Color.FromArgb(240, 180, 30, 30))
                : new SolidColorBrush(Color.FromArgb(240, 20, 30, 50)),
            BorderBrush = new SolidColorBrush(isError
                ? Color.FromRgb(255, 100, 100)
                : Color.FromRgb(0, 220, 200)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6, 10, 6),
            Child = _bboxResultText,
            Effect = new DropShadowEffect
            {
                BlurRadius = 12, ShadowDepth = 0,
                Color = isError ? Colors.Red : Color.FromRgb(0, 220, 200),
                Opacity = 0.7
            },
            Tag = "sam_manual_mask",
            IsHitTestVisible = false
        };
        // Position: oben rechts auf Canvas
        Canvas.SetLeft(_bboxResultPanel, OverlayCanvas.ActualWidth - 380);
        Canvas.SetTop(_bboxResultPanel, 12);
        OverlayCanvas.Children.Add(_bboxResultPanel);
    }

    private void AppendToBboxResultPanel(string moreText, bool isError)
    {
        if (_bboxResultText == null) return;
        _bboxResultText.Text += "\n" + moreText;
        if (isError && _bboxResultPanel != null)
            _bboxResultPanel.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 100, 100));
    }
}
