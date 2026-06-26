using System.Windows.Controls;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>Zone-Dots und Konfidenz-Texte in der Event-ListBox einfaerben.</summary>
    private void ColorizeCodingEventListItems()
    {
        CodingEventListItemColorizeWorkflow.Execute(
            new CodingEventListItemColorizeWorkflowRequest(LstCodingEvents.Items.Count),
            new CodingEventListItemColorizeWorkflowActions(
                TryApplyItem: index =>
                {
                    if (LstCodingEvents.ItemContainerGenerator.ContainerFromIndex(index) is not ListBoxItem container)
                        return false;
                    if (LstCodingEvents.Items[index] is not CodingEvent ev)
                        return false;

                    var zoneDot = FindCodingChild<System.Windows.Shapes.Ellipse>(container, "ZoneDot");
                    var confText = FindCodingChild<TextBlock>(container, "TxtConfidence");
                    var statusIcon = FindCodingChild<TextBlock>(container, "TxtStatusIcon");

                    CodingEventListItemControls.Apply(zoneDot, confText, statusIcon, ev);
                    return true;
                },
                RefreshHighlights: ApplyCodingProtocolMatchListHighlights));
    }
}
