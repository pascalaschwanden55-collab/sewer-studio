using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Nova-Etappe 2: einheitlicher Seitenkopf fuer alle Seiten. Titel im PageTitle-Stil, ein
/// optionaler kleiner Untertitel daneben (kollabiert bei leerem Text) und rechts ein freier
/// Aktionsbereich (<see cref="Aktionen"/>, ContentProperty) fuer bestehende Kopf-Knoepfe.
/// Lookless Control (Style/ControlTemplate in Controls/NovaPageHeader.xaml, analog zu
/// <see cref="StatusHost"/>): ein UserControl mit eigenem x:Class wuerde die WPF-Namescope fuer
/// per x:Name referenzierte Elemente (z. B. eine ComboBox im Aktionen-Inhalt) sperren.
/// </summary>
[ContentProperty(nameof(Aktionen))]
public class NovaPageHeader : Control
{
    static NovaPageHeader()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(NovaPageHeader), new FrameworkPropertyMetadata(typeof(NovaPageHeader)));
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(NovaPageHeader), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(NovaPageHeader), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty AktionenProperty =
        DependencyProperty.Register(nameof(Aktionen), typeof(object), typeof(NovaPageHeader), new PropertyMetadata(null));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

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
