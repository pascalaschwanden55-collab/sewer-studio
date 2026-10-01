using System.Windows;
using AuswertungPro.Next.UI.ViewModels;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class XtfLieferungWindow : Window
{
    public XtfLieferungWindow(XtfLieferungViewModel model)
    {
        InitializeComponent(); DataContext = model;
        Closing += (_, e) =>
        {
            if (System.Windows.Input.Keyboard.FocusedElement is System.Windows.Controls.TextBox text)
                text.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
            e.Cancel = !model.DarfSchliessen();
        };
    }
}
