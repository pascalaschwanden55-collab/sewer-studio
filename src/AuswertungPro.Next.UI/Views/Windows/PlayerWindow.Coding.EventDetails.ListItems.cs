using System.Windows.Controls;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>Zone-Dots und Konfidenz-Texte in der Event-ListBox einfaerben.</summary>
    private void ColorizeCodingEventListItems()
    {
        for (int i = 0; i < LstCodingEvents.Items.Count; i++)
        {
            if (LstCodingEvents.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem container) continue;
            if (LstCodingEvents.Items[i] is not CodingEvent ev) continue;

            var zoneDot = FindCodingChild<System.Windows.Shapes.Ellipse>(container, "ZoneDot");
            var confText = FindCodingChild<TextBlock>(container, "TxtConfidence");
            var statusIcon = FindCodingChild<TextBlock>(container, "TxtStatusIcon");

            CodingEventListItemControls.Apply(zoneDot, confText, statusIcon, ev);
        }

        ApplyCodingProtocolMatchListHighlights();
    }
}
