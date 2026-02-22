using System.Windows;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class TrainingCenterWindow : Window
{
    public TrainingCenterViewModel Vm { get; }

    public TrainingCenterWindow()
    {
        InitializeComponent();

        Vm = new TrainingCenterViewModel(
            new TrainingCenterStore(),
            new TrainingCenterImportService());

        DataContext = Vm;

        Loaded += async (_, __) => await Vm.LoadAsync();
    }
}
