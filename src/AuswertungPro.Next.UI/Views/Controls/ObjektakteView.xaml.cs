using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuswertungPro.Next.UI.ViewModels;

namespace AuswertungPro.Next.UI.Views.Controls;

/// <summary>Dieselben Nova-Felder in der Aufklappliste und im optionalen Objektaktenfenster.</summary>
public partial class ObjektakteView : UserControl
{
    public static readonly DependencyProperty SpaltenProperty = DependencyProperty.Register(
        nameof(Spalten), typeof(int), typeof(ObjektakteView), new PropertyMetadata(2));
    public int Spalten { get => (int)GetValue(SpaltenProperty); set => SetValue(SpaltenProperty, value); }

    /// <summary>Feldspalten nach Breite (11.09.2026, «alles kompakter»): eine Spalte braucht rund 400 px
    /// (130 Beschriftung + Eingabe); auf Full HD sind das vier. Die eine Regel fuer Aufklappliste und Fenster.</summary>
    public static int SpaltenFuerBreite(double breite) => breite switch
    {
        < 700 => 1,
        < 1100 => 2,
        < 1500 => 3,
        _ => 4,
    };

    public ObjektakteView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Spalten = SpaltenFuerBreite(ActualWidth);
        Loaded += (_, _) => (DataContext as ObjektakteViewModel)?.AktualisiereFelder();
        Unloaded += (_, _) =>
        {
            UebernehmeEingabe(this);
            (DataContext as ObjektakteViewModel)?.SpeichereAnsicht();
        };
        // Vor Themen-, Objekt- und Registerwechseln die letzte Eingabe sichern.
        AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler((_, _) => UebernehmeEingabe(this)), true);
    }

    public static void UebernehmeEingabe(FrameworkElement bereich)
    {
        if (bereich.IsKeyboardFocusWithin && Keyboard.FocusedElement is TextBox text)
        {
            var combo = Behaviors.VisualTreeSafe.FindAncestor<ComboBox>(text);
            if (combo is { IsEditable: true }) combo.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
            text.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }
    }

    private void Mehr_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }
    }
}
