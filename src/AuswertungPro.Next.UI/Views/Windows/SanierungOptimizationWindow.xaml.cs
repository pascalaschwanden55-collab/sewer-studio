using System.Net.Http;
using System.Windows;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Sanierung;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class SanierungOptimizationWindow : Window
{
    public SanierungOptimizationViewModel Vm { get; }

    public SanierungOptimizationWindow(SanierungOptimizationViewModel vm)
    {
        InitializeComponent();
        Vm = vm;
        DataContext = vm;
        vm.CloseRequested += () => Close();
    }
}
