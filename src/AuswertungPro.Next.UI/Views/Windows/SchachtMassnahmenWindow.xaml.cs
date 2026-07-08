using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Einfaches Schacht-Sanierungsmassnahmen-Fenster: klickbare Liste -> Auswahl -> Uebernehmen.
/// Kein NPK. Das ViewModel haelt die gesamte Logik; das Fenster reagiert nur auf CloseRequested
/// und den Doppelklick auf die Liste.
/// </summary>
public partial class SchachtMassnahmenWindow : Window
{
    public SchachtMassnahmenWindow(SchachtMassnahmenViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.CloseRequested += Close;
    }

    private void KatalogList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        _ = e;
        if (DataContext is SchachtMassnahmenViewModel vm
            && sender is ListBox list
            && list.SelectedItem is SchachtMassnahmeKatalogEintrag eintrag)
        {
            vm.HinzufuegenCommand.Execute(eintrag);
        }
    }
}
