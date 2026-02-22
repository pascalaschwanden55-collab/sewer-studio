using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class ProjectPage : System.Windows.Controls.UserControl
{
    public ProjectPage()
    {
        InitializeComponent();
    }

    private void ComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && sender is ComboBox combo)
        {
            combo.IsDropDownOpen = true;
            e.Handled = true;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        window?.Close();
    }
}
