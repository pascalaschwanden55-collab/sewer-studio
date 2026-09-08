using System;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Controls;

namespace AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

/// <summary>
/// Zeigt die Haltungsgrafik des Haltungsprotokolls — dieselbe Zeichnung wie im PDF, nur in den
/// Farben des Programms. Das SVG kommt aus <see cref="HaltungsgrafikAnsichtBuilder"/>, gezeichnet
/// wird es vom <see cref="SvgTeilmengeZeichner"/>.
///
/// Live: Ein Feldwechsel am Datensatz (Laenge, Schaechte, Inspektionsrichtung) und jede Aenderung
/// der Protokollliste zeichnen neu. Mehrere Meldungen kurz hintereinander ergeben genau einen
/// Neuaufbau, weil die Anforderung auf einen Dispatcher-Durchlauf gebuendelt wird.
/// </summary>
public partial class HaltungsgrafikControl : UserControl
{
    private INotifyCollectionChanged? _abonnierteEintraege;
    private HaltungRecord? _abonnierterDatensatz;
    private bool _neuzeichnenAngefordert;

    public HaltungsgrafikControl()
    {
        InitializeComponent();
        Loaded += (_, _) => FordereNeuzeichnenAn();
        Unloaded += (_, _) =>
        {
            AbmeldenVonEintraegen();
            AbmeldenVonDatensatz();
        };
    }

    public static readonly DependencyProperty RecordProperty = DependencyProperty.Register(
        nameof(Record), typeof(HaltungRecord), typeof(HaltungsgrafikControl),
        new PropertyMetadata(null, OnRecordChanged));

    /// <summary>Gewaehlte Haltung; ohne Datensatz bleibt die Flaeche leer.</summary>
    public HaltungRecord? Record
    {
        get => (HaltungRecord?)GetValue(RecordProperty);
        set => SetValue(RecordProperty, value);
    }

    public static readonly DependencyProperty EntriesProperty = DependencyProperty.Register(
        nameof(Entries), typeof(IEnumerable), typeof(HaltungsgrafikControl),
        new PropertyMetadata(null, OnEntriesChanged));

    /// <summary>
    /// Protokolleintraege der Auswahl. Die Grafik liest ihre Eintraege selbst aus dem
    /// Protokoll des Datensatzes; diese Bindung dient als Anlass zum Neuzeichnen, weil die
    /// Sammlung beim Haltungswechsel dieselbe Instanz bleibt.
    /// </summary>
    public IEnumerable? Entries
    {
        get => (IEnumerable?)GetValue(EntriesProperty);
        set => SetValue(EntriesProperty, value);
    }

    public static readonly DependencyProperty CatalogProperty = DependencyProperty.Register(
        nameof(Catalog), typeof(ICodeCatalogProvider), typeof(HaltungsgrafikControl),
        new PropertyMetadata(null, OnNeuZeichnen));

    /// <summary>Aktiver Codekatalog fuer die Klartexte; wird von der Seite gesetzt.</summary>
    public ICodeCatalogProvider? Catalog
    {
        get => (ICodeCatalogProvider?)GetValue(CatalogProperty);
        set => SetValue(CatalogProperty, value);
    }

    public static readonly DependencyProperty FlowDownProperty = DependencyProperty.Register(
        nameof(FlowDown), typeof(bool?), typeof(HaltungsgrafikControl),
        new PropertyMetadata(null, OnNeuZeichnen));

    /// <summary>
    /// Fliessrichtung als Vorgabe. <c>null</c> heisst: aus dem Feld <c>Inspektionsrichtung</c>
    /// ableiten — dieselbe Regel wie im PDF-Weg.
    /// </summary>
    public bool? FlowDown
    {
        get => (bool?)GetValue(FlowDownProperty);
        set => SetValue(FlowDownProperty, value);
    }

    public static readonly DependencyProperty SvgHoeheProperty = DependencyProperty.Register(
        nameof(SvgHoehe), typeof(int), typeof(HaltungsgrafikControl),
        new PropertyMetadata(700, OnNeuZeichnen));

    /// <summary>
    /// Zeichenhoehe der Grafik in SVG-Einheiten (Breite ist fest). Standard ist die Hoehe des
    /// PDF-Wegs; mehr Hoehe gibt den Beschriftungszeilen mehr Platz.
    /// </summary>
    public int SvgHoehe
    {
        get => (int)GetValue(SvgHoeheProperty);
        set => SetValue(SvgHoeheProperty, value);
    }

    private static readonly DependencyPropertyKey SymbolAnzahlPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(SymbolAnzahl), typeof(int), typeof(HaltungsgrafikControl), new PropertyMetadata(0));

    public static readonly DependencyProperty SymbolAnzahlProperty = SymbolAnzahlPropertyKey.DependencyProperty;

    /// <summary>Anzahl der Hinweisflaechen ueber den Beobachtungen (fuer Tests und Waechter).</summary>
    public int SymbolAnzahl => (int)GetValue(SymbolAnzahlProperty);

    private static void OnNeuZeichnen(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((HaltungsgrafikControl)d).FordereNeuzeichnenAn();

    private static void OnRecordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (HaltungsgrafikControl)d;
        control.AbmeldenVonDatensatz();
        if (e.NewValue is HaltungRecord record)
        {
            record.PropertyChanged += control.OnDatensatzGeaendert;
            control._abonnierterDatensatz = record;
        }

        control.FordereNeuzeichnenAn();
    }

    private static void OnEntriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (HaltungsgrafikControl)d;
        control.AbmeldenVonEintraegen();
        if (e.NewValue is INotifyCollectionChanged sammlung)
        {
            sammlung.CollectionChanged += control.OnEintraegeGeaendert;
            control._abonnierteEintraege = sammlung;
        }

        control.FordereNeuzeichnenAn();
    }

    private void OnEintraegeGeaendert(object? sender, NotifyCollectionChangedEventArgs e)
        => FordereNeuzeichnenAn();

    private void OnDatensatzGeaendert(object? sender, PropertyChangedEventArgs e)
        => FordereNeuzeichnenAn();

    private void AbmeldenVonEintraegen()
    {
        if (_abonnierteEintraege is null)
            return;
        _abonnierteEintraege.CollectionChanged -= OnEintraegeGeaendert;
        _abonnierteEintraege = null;
    }

    private void AbmeldenVonDatensatz()
    {
        if (_abonnierterDatensatz is null)
            return;
        _abonnierterDatensatz.PropertyChanged -= OnDatensatzGeaendert;
        _abonnierterDatensatz = null;
    }

    /// <summary>
    /// Buendelt mehrere Anlaesse (Feld, Protokoll, Katalog) zu einem Neuaufbau. Die Meldung des
    /// Datensatzes kommt auf dem setzenden Thread; die Zeichenflaeche gehoert dem UI-Thread.
    /// </summary>
    private void FordereNeuzeichnenAn()
    {
        if (_neuzeichnenAngefordert)
            return;

        _neuzeichnenAngefordert = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _neuzeichnenAngefordert = false;
            Zeichne();
        }));
    }

    /// <summary>
    /// Zeichnet sofort und nimmt eine gebuendelte Anforderung zurueck. Gedacht fuer Tests: Ohne
    /// Fenster laeuft keine Nachrichtenschleife, deshalb wuerde die Warteschlange nie abgearbeitet.
    /// </summary>
    internal void ZeichneJetzt()
    {
        _neuzeichnenAngefordert = false;
        Zeichne();
    }

    private void Zeichne()
    {
        Buehne.Child = null;
        SetValue(SymbolAnzahlPropertyKey, 0);

        if (Record is null)
        {
            ZeigeHinweis(null);
            return;
        }

        var ansicht = HaltungsgrafikAnsichtBuilder.Baue(Record, Catalog, SvgHoehe, FlowDown);
        if (ansicht is null)
        {
            ZeigeHinweis("Ohne erfasste Haltungslänge gibt es keinen Massstab für die Grafik.");
            return;
        }

        try
        {
            var flaeche = SvgTeilmengeZeichner.Zeichne(ansicht.Svg, this);
            ErgaenzeHinweisflaechen(flaeche, ansicht);
            Buehne.Child = flaeche;
            SetValue(SymbolAnzahlPropertyKey, ansicht.Marken.Count);
            ZeigeHinweis(null);
        }
        catch (NotSupportedException ex)
        {
            // Ein neues SVG-Element im Bauer darf die Seite nicht abstuerzen lassen, aber es
            // darf auch nicht still verschwinden. Der Waechtertest faengt den Fall vorher ab.
            ZeigeHinweis("Die Haltungsgrafik kann nicht gezeichnet werden: " + ex.Message);
        }
    }

    /// <summary>Unsichtbare Flaechen ueber den Beobachtungen: nur fuer den Hinweistext.</summary>
    private static void ErgaenzeHinweisflaechen(Canvas flaeche, HaltungsgrafikAnsicht ansicht)
    {
        foreach (var marke in ansicht.Marken)
        {
            var feld = new Rectangle
            {
                Width = marke.Breite,
                Height = marke.Hoehe,
                Fill = Brushes.Transparent,
                ToolTip = marke.Tooltip
            };
            Canvas.SetLeft(feld, marke.X);
            Canvas.SetTop(feld, marke.Y);
            flaeche.Children.Add(feld);
        }
    }

    private void ZeigeHinweis(string? text)
    {
        Hinweis.Text = text ?? string.Empty;
        Hinweis.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }
}
