using System.Windows;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class CombinedOfferWindow : Window
{
    public CombinedOfferWindow()
    {
        InitializeComponent();
        WindowStateManager.Track(this);
    }
}
