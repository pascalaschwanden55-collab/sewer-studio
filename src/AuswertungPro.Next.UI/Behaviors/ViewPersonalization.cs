using System.Windows;

namespace AuswertungPro.Next.UI.Behaviors;

/// <summary>
/// Zentraler Ausroll-Hook fuer das Einstell-System. Der <c>ViewKey</c> wird EINMAL
/// am Seiten-/Fenster-Root im XAML gesetzt und VERERBT sich in den ganzen visuellen Baum
/// (FrameworkPropertyMetadataOptions.Inherits). Sub-Behaviors (Grid, Splitter) lesen ihn
/// und kombinieren ihn mit ihrem lokalen Schluessel zu einem stabilen Persistenzpfad —
/// ohne GetType().Name-Kollision.
///
/// Beispiel (am UserControl-Root):
///   behaviors:ViewPersonalization.ViewKey="BuilderPage"
/// </summary>
public static class ViewPersonalization
{
    public static readonly DependencyProperty ViewKeyProperty =
        DependencyProperty.RegisterAttached(
            "ViewKey",
            typeof(string),
            typeof(ViewPersonalization),
            new FrameworkPropertyMetadata(
                defaultValue: null,
                flags: FrameworkPropertyMetadataOptions.Inherits));

    public static void SetViewKey(DependencyObject element, string? value)
        => element.SetValue(ViewKeyProperty, value);

    public static string? GetViewKey(DependencyObject element)
        => (string?)element.GetValue(ViewKeyProperty);
}
