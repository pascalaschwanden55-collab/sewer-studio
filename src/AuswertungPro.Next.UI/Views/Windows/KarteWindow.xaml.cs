using System.Windows;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class KarteWindow : Window
{
    public KarteWindow()
    {
        InitializeComponent();
        WindowStateManager.Track(this);
    }
}
