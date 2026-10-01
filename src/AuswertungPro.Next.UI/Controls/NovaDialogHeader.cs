using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Fensterkopf fuer Dialoge/Zusatzfenster (Optik und Bedienung professionell, 28.09.2026,
/// Aufgabe 3): Titel oben, darunter ein UMBRECHENDER Untertitel (der bisherige Einleitungssatz
/// des Fensters) und rechts ein optionaler Aktionsbereich. Ergaenzt
/// <see cref="NovaPageHeader"/> (Seitenkopf, Untertitel INLINE neben dem Titel, kein
/// Zeilenumbruch, Ellipsis) gezielt fuer FENSTER: die meisten Fenster-Einleitungssaetze sind
/// ganze erklaerende Saetze, die als einzeilig abgeschnittener Untertitel unlesbar wuerden. Regel
/// fuer neue/umgestellte Fenster: Seiten verwenden <see cref="NovaPageHeader"/>, Fenster
/// (Window) verwenden <see cref="NovaDialogHeader"/> - ein einheitliches, mechanisch
/// uebertragbares Muster je Elementart.
/// Lookless Control (Style/ControlTemplate in Theme/Controls.xaml, gleiches Muster wie
/// <see cref="NovaPageHeader"/> und <see cref="StatusHost"/>): ein UserControl mit eigenem
/// x:Class wuerde die WPF-Namescope fuer per x:Name referenzierte Elemente im Aktionen-Inhalt
/// sperren.
/// </summary>
[ContentProperty(nameof(Aktionen))]
public class NovaDialogHeader : Control
{
    static NovaDialogHeader()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(NovaDialogHeader), new FrameworkPropertyMetadata(typeof(NovaDialogHeader)));
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(NovaDialogHeader), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(NovaDialogHeader), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty AktionenProperty =
        DependencyProperty.Register(nameof(Aktionen), typeof(object), typeof(NovaDialogHeader), new PropertyMetadata(null));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Der bisherige Einleitungssatz des Fensters. Wird umbrechend UNTER dem Titel
    /// dargestellt und kollabiert vollstaendig, wenn er leer ist (kein leerer Rand).</summary>
    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public object? Aktionen
    {
        get => GetValue(AktionenProperty);
        set => SetValue(AktionenProperty, value);
    }
}
