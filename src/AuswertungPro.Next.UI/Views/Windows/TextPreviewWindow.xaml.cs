using System.Windows;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class TextPreviewWindow : Window
{
    public TextPreviewWindow(string title, string content)
    {
        InitializeComponent();
        WindowStateManager.Track(this);
        Title = string.IsNullOrWhiteSpace(title) ? "Text" : title;
        ContentBox.Text = content ?? string.Empty;
    }
}
