using System.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

// Slice 8a Pause-Confirm Step 2 — Click-Handler-Stubs fuer das in
// CodingModeWindow.xaml deklarierte Confirmation-Panel.
//
// Step 3 fuellt die Bodies (CompleteConfirmation + Edit-Side-Effect:
// SelectedDefect = PendingConfirmationEvent), Step 4 verdrahtet den Loop.
// Step 2 liefert nur die kompilierfaehige Hülle, damit das Panel sichtbar
// ans VM bindet ohne dass ein Click crashen koennte.
public partial class CodingModeWindow
{
    private void ConfirmAccept_Click(object sender, RoutedEventArgs e)
    {
        // Step 3: _vm.CompleteConfirmation(CodingUserDecision.Accepted)
    }

    private void ConfirmEdit_Click(object sender, RoutedEventArgs e)
    {
        // Step 3: _vm.SelectedDefect = _vm.PendingConfirmationEvent;
        //         _vm.CompleteConfirmation(CodingUserDecision.AcceptedWithEdit);
    }

    private void ConfirmReject_Click(object sender, RoutedEventArgs e)
    {
        // Step 3: _vm.CompleteConfirmation(CodingUserDecision.Rejected)
    }
}
