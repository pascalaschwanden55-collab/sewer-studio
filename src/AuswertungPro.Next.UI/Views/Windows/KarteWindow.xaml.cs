using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class KarteWindow : Window
{
    public KarteWindow()
    {
        InitializeComponent();
        WindowStateManager.Track(this);
    }

    public KarteWindow(UIElement? content) : this()
    {
        if (content is not null)
            SetContent(content);
    }

    public void SetContent(UIElement content)
    {
        ContentHost.Children.Clear();
        ContentHost.Children.Add(content);
    }

    public UIElement? TakeContent()
    {
        if (ContentHost.Children.Count == 0)
            return null;

        var content = ContentHost.Children[0];
        ContentHost.Children.Clear();
        return content;
    }
}
