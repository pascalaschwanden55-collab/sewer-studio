using System.Windows;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>Gestaltung auf losgeloesten Vorschaukarten; Schliessen ohne Speichern verwirft sie.</summary>
public partial class AufklappLayoutWindow : Window
{
    private readonly IReadOnlyList<RecordDetailGroup> _standard;
    internal RecordDetailLayout? Ergebnis { get; private set; }

    internal AufklappLayoutWindow(IReadOnlyList<RecordDetailGroup> standard, DetailLayoutSettings? layout)
    {
        InitializeComponent();
        _standard = standard;
        Details.Groups = AufklappDetailLayout.Vorschau(standard, RecordDetailLayoutSettingsMapper.ToLayout(layout));
        var area = SystemParameters.WorkArea;
        Width = Math.Min(Width, area.Width - 32);
        Height = Math.Min(Height, area.Height - 32);
    }

    private void Standard_Click(object sender, RoutedEventArgs e)
        => Details.Groups = AufklappDetailLayout.Vorschau(_standard, RecordDetailLayout.Empty);

    private void Speichern_Click(object sender, RoutedEventArgs e)
    {
        Ergebnis = RecordDetailLayoutApplier.Capture(Details.Groups!);
        DialogResult = true;
    }

    internal static RecordDetailLayout? Bearbeite(DependencyObject owner,
        IReadOnlyList<RecordDetailGroup> standard, DetailLayoutSettings? layout)
    {
        var fenster = new AufklappLayoutWindow(standard, layout) { Owner = GetWindow(owner) };
        return fenster.ShowDialog() == true ? fenster.Ergebnis : null;
    }
}
