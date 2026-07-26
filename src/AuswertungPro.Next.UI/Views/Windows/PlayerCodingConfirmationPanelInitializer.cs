using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public static class PlayerCodingConfirmationPanelInitializer
{
    public static void Initialize(
        CodingConfirmationPanelControlsOwner owner,
        Border panel,
        Shape ampel,
        TextBlock code,
        TextBlock confidence,
        TextBlock description,
        TextBlock detail,
        FrameworkElement saveErrorPanel,
        TextBlock saveErrorText)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(ampel);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(confidence);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(saveErrorPanel);
        ArgumentNullException.ThrowIfNull(saveErrorText);

        owner.Initialize(
            new CodingConfirmationPanelControls(
                panel,
                ampel,
                code,
                confidence,
                description,
                detail,
                saveErrorPanel,
                saveErrorText));
    }
}
