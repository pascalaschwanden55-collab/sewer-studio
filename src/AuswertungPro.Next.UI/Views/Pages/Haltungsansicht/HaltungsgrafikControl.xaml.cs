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
    private bool _neuzeichnenAusstehend;

    public HaltungsgrafikControl()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            // Dieselbe Instanz kann nach Unloaded wieder in die Seite eingesetzt werden.
            AbmeldenVonDatensatz();
            if (Record is { } record)
            {
                record.PropertyChanged += OnDatensatzGeaendert;
                _abonnierterDatensatz = record;
            }
            AbmeldenVonEintraegen();
            if (Entries is INotifyCollectionChanged sammlung)
            {
                sammlung.CollectionChanged += OnEintraegeGeaendert;
                _abonnierteEintraege = sammlung;
            }
            FordereNeuzeichnenAn();
        };
        IsVisibleChanged += OnIsVisibleChanged;
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

    public static readonly DependencyProperty NurRohrProperty = DependencyProperty.Register(
        nameof(NurRohr), typeof(bool), typeof(HaltungsgrafikControl),
        new PropertyMetadata(false, OnNeuZeichnen));

    /// <summary>
    /// Zeichnet nur die Rohrsaeule (Rohr, Meter-Skala, Symbole, Schachtknoten, Fliesspfeil) ohne
    /// die Beschriftungstabelle. In einer schmalen Spalte ist die volle Grafik unlesbar klein;
    /// dort ist die Schadenliste daneben die Legende.
    /// </summary>
    public bool NurRohr
    {
        get => (bool)GetValue(NurRohrProperty);
        set => SetValue(NurRohrProperty, value);
    }

    public static readonly DependencyProperty SvgHoeheProperty = DependencyProperty.Register(
        nameof(SvgHoehe), typeof(int?), typeof(HaltungsgrafikControl),
        new PropertyMetadata(null, OnNeuZeichnen));

    /// <summary>
    /// Zeichenhoehe der Grafik in SVG-Einheiten (die Breite ergibt sich aus <see cref="NurRohr"/>).
    /// <c>null</c> laesst <see cref="HaltungsgrafikAnsichtBuilder.Baue"/> dieselbe Standardhoehe
    /// wie den PDF-Weg waehlen (gestaffelt nach Anzahl Eintraege statt fest 700). Sie bestimmt
    /// zusammen mit der Anzeigehoehe den Massstab: Ist die Zeichnung nicht groesser als die
    /// Flaeche, bleibt die Schrift lesbar. Die schmale Rohrsaeule der Uebersicht setzt deshalb
    /// bewusst eine eigene, auf ihre feste Anzeigehoehe abgestimmte Zahl statt der Vorgabe.
    /// </summary>
    public int? SvgHoehe
    {
        get => (int?)GetValue(SvgHoeheProperty);
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
    /// Datensatzes kommt auf dem setzenden Thread; <see cref="_neuzeichnenAngefordert"/> gehoert
    /// dagegen dem UI-Thread und wird deshalb erst dort angefasst — ein Aufruf von aussen
    /// wechselt zuerst dorthin.
    /// </summary>
    private void FordereNeuzeichnenAn()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(FordereNeuzeichnenAn));
            return;
        }

        if (_neuzeichnenAngefordert)
            return;

        _neuzeichnenAngefordert = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _neuzeichnenAngefordert = false;

            // Ohne sichtbare Flaeche (z.B. die Uebersicht zeigt gerade den Leerzustand) lohnt
            // sich das Xml-Parsen und Formen-Erzeugen nicht. Der Hintergrund-Durchlauf laeuft
            // NACH Layout und Trigger-Auswertung, IsVisible spiegelt hier schon den endgueltigen
            // Zustand. Nachgeholt wird beim naechsten Sichtbarwerden ueber OnIsVisibleChanged.
            if (!IsVisible)
            {
                _neuzeichnenAusstehend = true;
                return;
            }

            Zeichne();
        }));
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!IsVisible || !_neuzeichnenAusstehend)
            return;

        _neuzeichnenAusstehend = false;
        FordereNeuzeichnenAn();
    }

    /// <summary>
    /// Zeichnet sofort und nimmt eine gebuendelte Anforderung zurueck. Gedacht fuer Tests: Ohne
    /// Fenster laeuft keine Nachrichtenschleife, deshalb wuerde die Warteschlange nie abgearbeitet.
    /// </summary>
    internal void ZeichneJetzt()
    {
        _neuzeichnenAngefordert = false;
        _neuzeichnenAusstehend = false;
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

        try
        {
            var ansicht = HaltungsgrafikAnsichtBuilder.Baue(Record, Catalog, SvgHoehe, FlowDown, NurRohr);
            if (ansicht is null)
            {
                ZeigeHinweis("Ohne erfasste Haltungslänge gibt es keinen Massstab für die Grafik.");
                return;
            }

            var flaeche = SvgTeilmengeZeichner.Zeichne(ansicht.Svg, this);
            ErgaenzeHinweisflaechen(flaeche, ansicht);
            Buehne.Child = flaeche;
            SetValue(SymbolAnzahlPropertyKey, ansicht.Marken.Count);
            ZeigeHinweis(null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Ein neues SVG-Element im Bauer, ein Steuerzeichen im Befundtext (XmlException) oder
            // ein kaputter Geometry-String (FormatException) duerfen die Oberflaeche beim blossen
            // Anzeigen nicht abstuerzen lassen. Der Waechtertest faengt neue Faelle vorher ab.
            Buehne.Child = null;
            SetValue(SymbolAnzahlPropertyKey, 0);
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
