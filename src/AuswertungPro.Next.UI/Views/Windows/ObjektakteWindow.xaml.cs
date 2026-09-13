using System.Windows;
using AuswertungPro.Next.UI.ViewModels;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class ObjektakteWindow : Window
{
    public static readonly DependencyProperty SpaltenProperty = DependencyProperty.Register(
        nameof(Spalten), typeof(int), typeof(ObjektakteWindow), new PropertyMetadata(2));
    public int Spalten { get => (int)GetValue(SpaltenProperty); set => SetValue(SpaltenProperty, value); }
    public ObjektakteWindow(ObjektakteViewModel model)
    {
        InitializeComponent(); DataContext = model;
        SizeChanged += (_, _) => Spalten = Controls.ObjektakteView.SpaltenFuerBreite(ActualWidth);
        Closing += (_, _) =>
        {
            if (System.Windows.Input.Keyboard.FocusedElement is System.Windows.Controls.TextBox text)
                text.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
        };
        Closed += (_, _) => model.SpeichereAnsicht();
    }
}
