using System.Windows;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>Editor-Fenster der globalen Schacht-Massnahmen-Liste (Name + Preis + Einheit).</summary>
public partial class SchachtMassnahmenKatalogEditorWindow : Window
{
    public SchachtMassnahmenKatalogEditorWindow(SchachtMassnahmenKatalogEditorViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.CloseRequested += ok =>
        {
            DialogResult = ok;
            Close();
        };
    }
}
