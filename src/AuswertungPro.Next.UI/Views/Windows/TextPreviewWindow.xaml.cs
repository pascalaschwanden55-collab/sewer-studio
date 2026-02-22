using System.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class TextPreviewWindow : Window
{
    public TextPreviewWindow(string title, string content)
    {
        InitializeComponent();
        Title = string.IsNullOrWhiteSpace(title) ? "Text" : title;
        ContentBox.Text = content ?? string.Empty;
    }
}
