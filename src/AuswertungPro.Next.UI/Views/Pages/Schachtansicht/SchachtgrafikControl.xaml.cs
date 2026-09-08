using System;
using System.Collections.Generic;
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

namespace AuswertungPro.Next.UI.Views.Pages.Schachtansicht;

/// <summary>
/// Zeigt die Schachtgrafik — ein senkrechter Schnitt durch den Schacht (WinCan-Art) mit Konus,
/// Schachtwand, Sohle, Tiefen-Masslinie, Innenmassen, angeschlossenen Haltungen und
/// Schadenssymbolen. Das SVG kommt aus <see cref="SchachtgrafikAnsichtBuilder"/>, gezeichnet wird
/// es vom <see cref="SvgTeilmengeZeichner"/> — dieselbe Technik wie
/// <c>Haltungsansicht.HaltungsgrafikControl</c>.
///
/// Live: Ein Feldwechsel am Datensatz (Tiefe, Masse, Schachtnummer) und jede In-Place-Aenderung
/// des Protokolls zeichnen neu. Mehrere Meldungen kurz hintereinander ergeben genau einen
/// Neuaufbau, weil die Anforderung auf einen Dispatcher-Durchlauf gebuendelt wird.
/// </summary>
public partial class SchachtgrafikControl : UserControl
{
    private SchachtRecord? _abonnierterDatensatz;
    private bool _neuzeichnenAngefordert;
    private bool _neuzeichnenAusstehend;

    public SchachtgrafikControl()
    {
        InitializeComponent();
        Loaded += (_, _) => FordereNeuzeichnenAn();
        IsVisibleChanged += OnIsVisibleChanged;
        Unloaded += (_, _) => AbmeldenVonDatensatz();
    }

    public static readonly DependencyProperty RecordProperty = DependencyProperty.Register(
        nameof(Record), typeof(SchachtRecord), typeof(SchachtgrafikControl),
        new PropertyMetadata(null, OnRecordChanged));

    /// <summary>Gewaehlter Schacht; ohne Datensatz bleibt die Flaeche leer.</summary>
    public SchachtRecord? Record
    {
        get => (SchachtRecord?)GetValue(RecordProperty);
        set => SetValue(RecordProperty, value);
    }

    public static readonly DependencyProperty HaltungenProperty = DependencyProperty.Register(
        nameof(Haltungen), typeof(IReadOnlyList<HaltungRecord>), typeof(SchachtgrafikControl),
        new PropertyMetadata(null, OnNeuZeichnen));

    /// <summary>
    /// Alle Haltungen des Projekts, von der Seite gereicht (kein Service-Locator im Control).
    /// Der Ansicht-Builder waehlt daraus selbst die an diesen Schacht angeschlossenen Zu- und
    /// Ablaeufe ueber <c>Schacht_unten</c>/<c>Schacht_oben</c>.
    /// </summary>
    public IReadOnlyList<HaltungRecord>? Haltungen
    {
        get => (IReadOnlyList<HaltungRecord>?)GetValue(HaltungenProperty);
        set => SetValue(HaltungenProperty, value);
    }

    public static readonly DependencyProperty CatalogProperty = DependencyProperty.Register(
        nameof(Catalog), typeof(ICodeCatalogProvider), typeof(SchachtgrafikControl),
        new PropertyMetadata(null, OnNeuZeichnen));

    /// <summary>Aktiver Codekatalog fuer die Klartexte der Schaeden; wird von der Seite gesetzt.</summary>
    public ICodeCatalogProvider? Catalog
    {
        get => (ICodeCatalogProvider?)GetValue(CatalogProperty);
        set => SetValue(CatalogProperty, value);
    }

    private static readonly DependencyPropertyKey SymbolAnzahlPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(SymbolAnzahl), typeof(int), typeof(SchachtgrafikControl), new PropertyMetadata(0));

    public static readonly DependencyProperty SymbolAnzahlProperty = SymbolAnzahlPropertyKey.DependencyProperty;

    /// <summary>Anzahl der Hinweisflaechen ueber den Schaeden (fuer Tests und Waechter).</summary>
    public int SymbolAnzahl => (int)GetValue(SymbolAnzahlProperty);

    private static void OnNeuZeichnen(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SchachtgrafikControl)d).FordereNeuzeichnenAn();

    private static void OnRecordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SchachtgrafikControl)d;
        control.AbmeldenVonDatensatz();
        if (e.NewValue is SchachtRecord record)
        {
            record.PropertyChanged += control.OnDatensatzGeaendert;
            control._abonnierterDatensatz = record;
        }

        control.FordereNeuzeichnenAn();
    }

    private void OnDatensatzGeaendert(object? sender, PropertyChangedEventArgs e)
        => FordereNeuzeichnenAn();

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
            var ansicht = SchachtgrafikAnsichtBuilder.Baue(Record, Haltungen, Catalog);
            if (ansicht is null)
            {
                ZeigeHinweis(null);
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
            // Ein neues SVG-Element im Bauer oder ein kaputter Geometry-String duerfen die
            // Oberflaeche beim blossen Anzeigen nicht abstuerzen lassen.
            Buehne.Child = null;
            SetValue(SymbolAnzahlPropertyKey, 0);
            ZeigeHinweis("Die Schachtgrafik kann nicht gezeichnet werden: " + ex.Message);
        }
    }

    /// <summary>Unsichtbare Flaechen ueber den Schaeden: nur fuer den Hinweistext.</summary>
    private static void ErgaenzeHinweisflaechen(Canvas flaeche, SchachtgrafikAnsicht ansicht)
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
