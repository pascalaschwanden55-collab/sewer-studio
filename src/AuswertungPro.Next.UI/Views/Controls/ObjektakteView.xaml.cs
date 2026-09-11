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

    public ObjektakteView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Spalten = ActualWidth < 880 ? 1 : 2;
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
