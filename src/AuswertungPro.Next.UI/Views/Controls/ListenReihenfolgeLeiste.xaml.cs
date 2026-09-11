using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AuswertungPro.Next.UI.Views.Controls;

public partial class ListenReihenfolgeLeiste : UserControl
{
    public ListenReihenfolgeLeiste() => InitializeComponent();

    public static readonly DependencyProperty IstAktivProperty = DependencyProperty.Register(
        nameof(IstAktiv), typeof(bool), typeof(ListenReihenfolgeLeiste),
        new PropertyMetadata(false, (d, _) => ((ListenReihenfolgeLeiste)d).AktivGeaendert?.Invoke(d, EventArgs.Empty)));
    public static readonly DependencyProperty AuswahlTextProperty = DependencyProperty.Register(
        nameof(AuswahlText), typeof(string), typeof(ListenReihenfolgeLeiste), new PropertyMetadata("Zeile auswählen"));
    public static readonly DependencyProperty HinweisProperty = DependencyProperty.Register(
        nameof(Hinweis), typeof(string), typeof(ListenReihenfolgeLeiste), new PropertyMetadata(string.Empty));

    public bool IstAktiv { get => (bool)GetValue(IstAktivProperty); set => SetValue(IstAktivProperty, value); }
    public string AuswahlText { get => (string)GetValue(AuswahlTextProperty); set => SetValue(AuswahlTextProperty, value); }
    public string Hinweis { get => (string)GetValue(HinweisProperty); set => SetValue(HinweisProperty, value); }

    public event EventHandler? AktivGeaendert;
    internal event Action<string>? PositionGewuenscht;

    private void Verschieben_Click(object sender, RoutedEventArgs e) => PositionGewuenscht?.Invoke(PositionBox.Text);
    private void Anfang_Click(object sender, RoutedEventArgs e) => PositionGewuenscht?.Invoke("1");
    private void Ende_Click(object sender, RoutedEventArgs e) => PositionGewuenscht?.Invoke("Ende");
    private void Fertig_Click(object sender, RoutedEventArgs e) => IstAktiv = false;
    private void Position_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        Verschieben_Click(sender, e);
        e.Handled = true;
    }
}
