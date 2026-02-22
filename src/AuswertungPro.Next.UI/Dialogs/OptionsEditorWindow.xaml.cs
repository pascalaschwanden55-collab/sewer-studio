using System.Windows;

namespace AuswertungPro.Next.UI.Dialogs
{
    public partial class OptionsEditorWindow : Window
    {
        public OptionsEditorWindow(OptionsEditorViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(vm.DialogResult) && vm.DialogResult.HasValue)
                {
                    DialogResult = vm.DialogResult;
                    Close();
                }
            };
        }
    }
}
