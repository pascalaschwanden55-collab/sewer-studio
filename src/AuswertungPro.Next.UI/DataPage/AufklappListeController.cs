using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Was <see cref="AufklappListeController{TListe,TRecord}"/> an einer Aufklapp-Liste braucht.
/// <c>HaltungAufklappListe</c> und <c>SchachtAufklappListe</c> erfuellen es mit ihren vorhandenen
/// Mitgliedern.
/// </summary>
public interface IAufklappListe<TRecord> where TRecord : class
{
    TRecord? Aufgeklappt { get; }
    System.Collections.IEnumerable? ItemsSource { get; }
    Func<TRecord, IReadOnlyList<RecordDetailGroup>>? DetailBuilder { get; set; }
    ObjektakteViewModel? Objektakte { get; set; }
    ListenReihenfolgeController Reihenfolge { get; }
    event EventHandler? AufgeklapptChanged;
    event EventHandler? AnsichtAnpassenRequested;
    void ZeigeThemen(IReadOnlyList<ThemaAnzeige>? themen);
    void KlappeZu();
}

/// <summary>
/// Nova, Aufklapp-Liste: verbindet die Liste mit dem Formular des aufgeklappten Datensatzes —
/// eine Fassung fuer Haltungen und Schaechte (Deepscan 02.10.2026, A2; vorher zwei Kopien, zu
/// 91 % gleich). Die Seiten liefern in <c>DataPageAufklappListeController</c> und
/// <c>SchaechteAufklappListeController</c> nur, was fachlich verschieden ist: Datensaetze,
/// Aenderungsschranke, Layout und — nur bei den Haltungen — den Konflikthinweis (W01).
///
/// Zwei Regeln tragen diese Klasse:
/// 1. Es gibt genau EIN Formular, naemlich das des aufgeklappten Datensatzes. Beim Wechsel wird der
///    bisherige <see cref="DataPageDetailLiveSync"/> entsorgt, sonst wuerde ein alter Datensatz
///    weiter in ein nicht mehr sichtbares Formular schreiben.
/// 2. Es gibt keinen zweiten Schreibweg. Die Felder kommen fertig aus dem Detail-Builder der
///    Seite (Haltungen mit der Konfliktregel der <c>DataPageDetailItemFactory</c>); hier wird nur
///    gebaut, angeschlossen und wieder entsorgt.
/// </summary>
public abstract class AufklappListeController<TListe, TRecord> : IDisposable
    where TListe : FrameworkElement, IAufklappListe<TRecord>
    where TRecord : class, INotifyPropertyChanged
{
    private readonly TListe _liste;
    private readonly Func<TRecord, IReadOnlyList<RecordDetailGroup>> _detailBuilder;

    private readonly ObjektaktenInlineBindung _objektakte = new();
    private DataPageDetailLiveSync? _sync;
    private bool _verdrahtet;

    protected AufklappListeController(TListe liste, Func<TRecord, IReadOnlyList<RecordDetailGroup>> detailBuilder)
    {
        _liste = liste ?? throw new ArgumentNullException(nameof(liste));
        _detailBuilder = detailBuilder ?? throw new ArgumentNullException(nameof(detailBuilder));
    }

    protected TListe Liste => _liste;

    /// <summary>Die Datensaetze der Seite (<c>Records</c>); null ohne ViewModel.</summary>
    protected abstract Collection<TRecord>? Datensaetze { get; }

    /// <summary>Das ViewModel als Bezug der Objektakte; null ohne ViewModel.</summary>
    protected abstract object? Seite { get; }

    /// <summary>Die Aenderungsschranke der Seite fuer das Verschieben.</summary>
    protected abstract bool DarfAendern { get; }

    /// <summary>Waehlt den Datensatz und verschiebt ihn auf die 1-basierte Position.</summary>
    protected abstract bool WaehleUndVerschiebe(TRecord record, int position);

    protected abstract ObjektakteViewModel? ErstelleObjektakte(TRecord record);

    protected abstract AppSettings? Einstellungen { get; }

    /// <summary>Haltungen: <c>DataPageLayout</c>, Schaechte: <c>SchaechtePageLayout</c>.</summary>
    protected abstract DataPageLayoutSettings Layout(AppSettings einstellungen);

    /// <summary>Feldwert fuer den Live-Abgleich (beide Datensaetze melden Fields[Name]/Fields).</summary>
    protected abstract string? Wert(TRecord record, string feld);

    /// <summary>Hinweis in der Kopfzeile des Formulars (W01). Nur die Haltungsliste zeigt einen.</summary>
    protected virtual void SetzeHinweis(string text)
    {
    }

    /// <summary>Einmalige Verdrahtung, unabhaengig vom ViewModel.</summary>
    public void Verdrahte()
    {
        if (_verdrahtet)
            return;

        // Der Builder gehoert der Seite; die Liste zeigt ihn nur an, gerufen wird er hier.
        _liste.DetailBuilder = _detailBuilder;
        _liste.Reihenfolge.Datensaetze = () => Datensaetze;
        _liste.Reihenfolge.DarfVerschieben = () => DarfAendern;
        _liste.Reihenfolge.Verschiebe = (eintrag, position) =>
            eintrag is TRecord record && DarfAendern && Datensaetze?.Contains(record) == true
            && WaehleUndVerschiebe(record, position);
        _liste.AufgeklapptChanged += OnAufgeklapptChanged;
        _liste.AnsichtAnpassenRequested += OnAnsichtAnpassen;
        _verdrahtet = true;
        // Ein nach Unloaded noch offenes Formular braucht wieder seinen Live-Abgleich.
        AktualisiereFormular();
    }

    /// <summary>
    /// Baut das Formular des aufgeklappten Datensatzes neu auf und schliesst den Live-Abgleich mit
    /// genau diesem Datensatz an. Ohne aufgeklappten Datensatz bleibt nichts stehen.
    ///
    /// Die Seite ruft das zusaetzlich bei einem Wechsel von <c>vm.Selected</c>: Eine Auswahl per
    /// Pfeiltaste klappt bewusst nicht auf, aber ein verschwundener Datensatz (geloescht,
    /// Projektwechsel) darf kein Formular hinterlassen.
    /// </summary>
    public void AktualisiereFormular()
    {
        Views.Controls.ObjektakteView.UebernehmeEingabe(_liste);
        _sync?.Dispose();
        _sync = null;
        SetzeHinweis(string.Empty);

        var record = _liste.Aufgeklappt;
        if (record is null || _liste.DetailBuilder is null)
        {
            _objektakte.Leere(_liste);
            _liste.Objektakte = null;
            _liste.ZeigeThemen(null);
            return;
        }

        if (!IstNochInDerListe(record))
        {
            // Geloescht oder Projektwechsel: nicht nur die Themen leeren, sondern wirklich
            // zuklappen. Sonst bliebe der Pfeil gedreht und die Liste behauptete, da sei noch
            // etwas offen. Der erneute AufgeklapptChanged laeuft oben mit record == null aus.
            _objektakte.Leere(_liste);
            _liste.Objektakte = null;
            _liste.ZeigeThemen(null);
            _liste.KlappeZu();
            return;
        }

        _liste.Objektakte = _objektakte.Aktualisiere(_liste, record, Seite, () => ErstelleObjektakte(record));
        var gruppen = _liste.DetailBuilder(record);
        _liste.ZeigeThemen(AufklappDetailLayout.Themen(gruppen, Einstellungen is { } s ? Layout(s).DetailLayout : null));
        _sync = new DataPageDetailLiveSync(record, feld => Wert(record, feld), gruppen);
    }

    public void AktualisiereImportwerte() => _liste.Objektakte?.AktualisiereFelder();

    /// <summary>
    /// Gehoert der aufgeklappte Datensatz noch zum Bestand? Gefragt werden beide Quellen, die es
    /// wissen koennen: das ViewModel, sobald die Seite eines gesetzt hat, und die Liste selbst.
    /// Es ist dieselbe Sammlung — die Seite bindet <c>Records</c> —, aber die Liste kann die
    /// Frage auch ohne ViewModel beantworten, und genau das braucht der Seitenaufbau.
    /// </summary>
    private bool IstNochInDerListe(TRecord record)
    {
        if (Datensaetze is { } datensaetze && !datensaetze.Contains(record))
            return false;

        return _liste.ItemsSource is not System.Collections.IEnumerable quelle || Enthaelt(quelle, record);
    }

    private static bool Enthaelt(System.Collections.IEnumerable quelle, TRecord record)
    {
        foreach (var eintrag in quelle)
        {
            if (ReferenceEquals(eintrag, record))
                return true;
        }

        return false;
    }

    private void OnAufgeklapptChanged(object? sender, EventArgs e) => AktualisiereFormular();

    private void OnAnsichtAnpassen(object? sender, EventArgs e)
    {
        if (_liste.Aufgeklappt is not { } record || Einstellungen is not { } settings)
            return;
        var layout = AufklappLayoutWindow.Bearbeite(_liste, _detailBuilder(record), Layout(settings).DetailLayout);
        if (layout is null)
            return;
        Layout(settings).DetailLayout = RecordDetailLayoutSettingsMapper.ToSettings(layout);
        settings.Save();
        AktualisiereFormular();
    }

    public void Dispose()
    {
        _objektakte.Leere(_liste);
        _liste.Objektakte = null;
        _liste.Reihenfolge.Beende();
        _liste.Reihenfolge.Datensaetze = null;
        _liste.Reihenfolge.DarfVerschieben = null;
        _liste.Reihenfolge.Verschiebe = null;
        if (_verdrahtet)
        {
            _liste.AufgeklapptChanged -= OnAufgeklapptChanged;
            _liste.AnsichtAnpassenRequested -= OnAnsichtAnpassen;
            _verdrahtet = false;
        }

        _sync?.Dispose();
        _sync = null;
        GC.SuppressFinalize(this);
    }
}
