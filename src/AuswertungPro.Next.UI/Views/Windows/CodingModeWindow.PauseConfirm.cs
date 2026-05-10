using System.Windows;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Windows;

// Slice 8a Pause-Confirm Step 3 — Click-Handler fuer das in
// CodingModeWindow.xaml deklarierte Confirmation-Panel (Step 2).
//
// Der Loop (Step 4) ruft BeginConfirmationAsync und await-tet die
// User-Entscheidung. Diese Click-Handler setzen das TaskCompletionSource
// per CompleteConfirmation — der Loop laeuft dann mit dem Result weiter.
//
// Edit-Sonderfall: Event in _vm.SelectedDefect schieben, damit das
// DefectDetailPanel den User direkt in den Edit-Modus bringt.
public partial class CodingModeWindow
{
    private void ConfirmAccept_Click(object sender, RoutedEventArgs e)
    {
        _vm.CompleteConfirmation(CodingUserDecision.Accepted);
    }

    private void ConfirmEdit_Click(object sender, RoutedEventArgs e)
    {
        // Edit-Pfad: User-Erfahrung wie alte PlayerWindow-Variante —
        // pending-Event ins SelectedDefect, damit das DefectDetailPanel
        // sofort die Edit-Bindings hat. Loop bekommt AcceptedWithEdit
        // und legt das Event in der Eventliste an (Step 4).
        var pending = _vm.PendingConfirmationEvent;
        if (pending is not null)
            _vm.SelectedDefect = pending;
        _vm.CompleteConfirmation(CodingUserDecision.AcceptedWithEdit);
    }

    private void ConfirmReject_Click(object sender, RoutedEventArgs e)
    {
        _vm.CompleteConfirmation(CodingUserDecision.Rejected);
    }
}
