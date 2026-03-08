using System.Windows;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class CostCalculationWindow : Window
{
    public CostCalculationWindow()
    {
        InitializeComponent();
        WindowStateManager.Track(this);
    }
}
