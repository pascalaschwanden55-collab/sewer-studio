using System.Windows;
using AuswertungPro.Next.Application.Xtf.Lieferung;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Services;

public static class XtfLieferungDialog
{
    public static void Zeige(IXtfLieferungsAblage ablage, IDialogService dialogs)
    {
        var w = new XtfLieferungWindow(new XtfLieferungViewModel(ablage, dialogs));
        var owner = System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive);
        if (owner is not null) w.Owner = owner;
        w.ShowDialog();
    }
}
