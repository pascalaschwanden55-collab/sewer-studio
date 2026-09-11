using System.Windows;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

public partial class HaltungsgrafikControl
{
    public static readonly DependencyProperty FotoOeffnenProperty = DependencyProperty.Register(
        nameof(FotoOeffnen), typeof(Action<IReadOnlyList<string>>), typeof(HaltungsgrafikControl),
        new PropertyMetadata(null, OnNeuZeichnen));

    public Action<IReadOnlyList<string>>? FotoOeffnen
    {
        get => (Action<IReadOnlyList<string>>?)GetValue(FotoOeffnenProperty);
        set => SetValue(FotoOeffnenProperty, value);
    }

    public static readonly DependencyProperty MitKlartextProperty = DependencyProperty.Register(
        nameof(MitKlartext), typeof(bool), typeof(HaltungsgrafikControl), new PropertyMetadata(false, OnNeuZeichnen));

    public bool MitKlartext
    {
        get => (bool)GetValue(MitKlartextProperty);
        set => SetValue(MitKlartextProperty, value);
    }

    private void KlartextGroesseGeaendert(object sender, SizeChangedEventArgs e)
    {
        if (MitKlartext) FordereNeuzeichnenAn();
    }

    private void ZeichneKlartext()
    {
        var bild = HaltungsgrafikKlartextZeichner.Zeichne(Record!, Catalog, this,
            Math.Max(230, ActualWidth - 18), Math.Max(300, ActualHeight - 4), FlowDown, FotoOeffnen);
        if (bild is null)
        {
            ZeigeHinweis("Ohne erfasste Haltungslänge gibt es keinen Massstab für die Grafik.");
            return;
        }
        KlartextBildlauf.Content = bild.Value.Flaeche;
        SetValue(SymbolAnzahlPropertyKey, bild.Value.Anzahl);
        ZeigeHinweis(null);
    }
}
