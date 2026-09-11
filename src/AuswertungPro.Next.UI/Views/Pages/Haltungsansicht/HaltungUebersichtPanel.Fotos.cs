using System.Windows;

namespace AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

public partial class HaltungUebersichtPanel
{
    public static readonly DependencyProperty FotoOeffnenProperty = DependencyProperty.Register(
        nameof(FotoOeffnen), typeof(Action<IReadOnlyList<string>>), typeof(HaltungUebersichtPanel));

    public Action<IReadOnlyList<string>>? FotoOeffnen
    {
        get => (Action<IReadOnlyList<string>>?)GetValue(FotoOeffnenProperty);
        set => SetValue(FotoOeffnenProperty, value);
    }
}
