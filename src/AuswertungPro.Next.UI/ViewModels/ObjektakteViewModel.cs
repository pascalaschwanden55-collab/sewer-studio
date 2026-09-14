using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.ViewModels;

public sealed class ObjektakteViewModel : ObservableObject
{
    private readonly ObjektaktenBearbeitung _bearbeitung;
    private readonly AppSettings _settings;
    private readonly Action _geaendert;
    private readonly Func<bool> _darfSchreiben;
    private ObjektAkte _auswahl;
    private string _thema = "Alle Felder";
    private string _suche = "";
    private bool _anpassen;
    private string _meldung = "Änderungen gehören zum Projekt. Bitte anschliessend speichern.";
    private IReadOnlyList<ObjektFeldViewModel> _felder = [];
    private IReadOnlyList<ObjektWebGisAbschnitt>? _abschnitte;
    private bool _alleOeffnen;

    private readonly Action<ObjektFeldViewModel>? _listeBearbeiten;

    public ObjektakteViewModel(ObjektaktenBearbeitung bearbeitung, AppSettings settings, Action geaendert,
        Func<bool> darfSchreiben, Action speichern, Action? exportieren = null, Func<bool>? importieren = null,
        Action<ObjektFeldViewModel>? listeBearbeiten = null,
        Func<Task<bool>>? geoShopErgaenzen = null, Action? geoShopDatei = null)
    {
        _bearbeitung = bearbeitung; _settings = settings; _geaendert = geaendert; _darfSchreiben = darfSchreiben;
        _listeBearbeiten = listeBearbeiten;
        _auswahl = bearbeitung.Wurzel;
        // Ob eine Objektart hier angelegt werden darf, sagt der Katalog - nicht eine zweite
        // Aufzaehlung in der Oberflaeche.
        NeuerDeckelCommand = new RelayCommand(() => Neu("deckel"), () => bearbeitung.DarfAnlegen("deckel") && darfSchreiben());
        NeueSanierungCommand = new RelayCommand(() => Neu("sanierung"), () => bearbeitung.DarfAnlegen("sanierung") && darfSchreiben());
        HauptdeckelCommand = new RelayCommand(() =>
        {
            if (!darfSchreiben()) return;
            bearbeitung.SetzeHauptdeckel(Auswahl); _geaendert(); LadeFelder();
        }, () => Auswahl.Art == "deckel" && darfSchreiben());
        SpeichernCommand = new RelayCommand(() => { if (darfSchreiben()) speichern(); });
        TrefferOeffnenCommand = new RelayCommand<ObjektAktenTreffer>(t => { if (t is not null) Auswahl = t.Akte; });
        ExportierenCommand = new RelayCommand(() => exportieren?.Invoke(), () => exportieren is not null);
        ImportierenCommand = new RelayCommand(() =>
        {
            if (!darfSchreiben() || importieren?.Invoke() != true) return;
            _auswahl = bearbeitung.Verbund.First(a => a.Id == _auswahl.Id);
            LadeFelder(); OnPropertyChanged(nameof(Objekte)); OnPropertyChanged(nameof(Auswahl)); _geaendert();
        }, () => importieren is not null && darfSchreiben());
        // «Fehlende Felder aus GeoShop-XTF» (11.09.2026): liest nur dieses Bauteil; Datei, Dialoge und
        // Uebernahme liegen beim Aufrufer, die Maske liest danach nur ihre Felder neu.
        GeoShopErgaenzenCommand = new AsyncRelayCommand(async () =>
        {
            if (geoShopErgaenzen is null || !darfSchreiben()) return;
            if (!await geoShopErgaenzen()) return;
            LadeFelder(); OnPropertyChanged(nameof(Objekte)); _geaendert();
            Meldung = "Aus der GeoShop-XTF übernommen. Bitte das Projekt speichern.";
        }, () => geoShopErgaenzen is not null && darfSchreiben());
        GeoShopDateiCommand = new RelayCommand(() => geoShopDatei?.Invoke(), () => geoShopDatei is not null);
        LadeFelder();
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(Gruppen))
            {
                _abschnitte = null;
                OnPropertyChanged(nameof(KopfFelder)); OnPropertyChanged(nameof(Abschnitte));
            }
            if (e.PropertyName is nameof(Listen) && _abschnitte is not null)
                ObjektaktenWebGisLayout.AktualisiereListen(_abschnitte, Listen);
        };
    }
    public string Titel => $"Objektakte · {_bearbeitung.Bezugsname(_bearbeitung.WurzelId)}";
    public string Stand => FieldCatalog.Objektfelder.Stand;
    public string Meldung { get => _meldung; private set => SetProperty(ref _meldung, value); }
    public string Tiefe => _bearbeitung.BerechneteTiefe();
    public IReadOnlyList<ObjektFeldViewModel> KopfFelder => _felder.Where(Passt)
        .Where(f => ObjektaktenWebGisLayout.Bereich(f.Feld) == "Kopf").ToArray();
    public IReadOnlyList<ObjektWebGisAbschnitt> Abschnitte
    {
        get
        {
            if (_abschnitte is not null) return _abschnitte;
            var abschnitte = ObjektaktenWebGisLayout.Erstelle(Auswahl.Art, _felder.Where(Passt), Listen, _settings, Sucht);
            foreach (var a in abschnitte) a.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(a.Offen) || !a.Offen || _alleOeffnen || Sucht) return;
                foreach (var andere in abschnitte.Where(x => x != a && x.Offen)) andere.Offen = false;
            };
            return _abschnitte = abschnitte;
        }
    }
    public IRelayCommand AlleAufCommand => new RelayCommand(() =>
    {
        _alleOeffnen = true;
        try { foreach (var a in Abschnitte) a.Offen = true; }
        finally { _alleOeffnen = false; }
    });
    public IRelayCommand AlleZuCommand => new RelayCommand(() => { foreach (var a in Abschnitte) a.Offen = false; });
    public bool NurFavoriten { get => Thema == "Meine Übersicht"; set { Thema = value ? "Meine Übersicht" : "Alle Felder"; OnPropertyChanged(); } }
    public IRelayCommand NeuerDeckelCommand { get; }
    public IRelayCommand NeueSanierungCommand { get; }
    public IRelayCommand HauptdeckelCommand { get; }
    public IRelayCommand SpeichernCommand { get; }
    public IRelayCommand TrefferOeffnenCommand { get; }
    public IEnumerable<ObjektAktenTreffer> Suchtreffer => ObjektaktenSuche.Finde(_bearbeitung, Suche).Take(50);
    public bool Sucht => !string.IsNullOrWhiteSpace(Suche);
    public IRelayCommand ExportierenCommand { get; }
    public IRelayCommand ImportierenCommand { get; }
    public IAsyncRelayCommand GeoShopErgaenzenCommand { get; }
    public IRelayCommand GeoShopDateiCommand { get; }
    public IReadOnlyList<ObjektWahl> Objekte => _bearbeitung.Verbund.Select(a => new ObjektWahl(a,
        a.Id == _bearbeitung.WurzelId ? _bearbeitung.Bezugsname(a.Id) : a.ToString())).ToArray();
    public ObjektAkte Auswahl
    {
        get => _auswahl;
        set { if (value is not null && SetProperty(ref _auswahl, value)) { LadeFelder(); HauptdeckelCommand.NotifyCanExecuteChanged(); } }
    }
    public IEnumerable<string> Themen => new[] { "Alle Felder", "Meine Übersicht" }.Concat(_felder.Select(f => f.Feld.Thema).Distinct());
    public string Thema { get => _thema; set { if (value is not null && SetProperty(ref _thema, value)) OnPropertyChanged(nameof(Gruppen)); } }
    public string Suche { get => _suche; set { if (SetProperty(ref _suche, value)) { OnPropertyChanged(nameof(Gruppen)); OnPropertyChanged(nameof(Listen)); OnPropertyChanged(nameof(Suchtreffer)); OnPropertyChanged(nameof(Sucht)); } } }
    public bool Anpassen { get => _anpassen; set { if (SetProperty(ref _anpassen, value)) OnPropertyChanged(nameof(Gruppen)); } }
    public IEnumerable<ObjektFeldGruppe> Gruppen => _felder.Where(Passt)
        .GroupBy(f => (f.Feld.Thema, f.Feld.Gruppe))
        .Select(g => new ObjektFeldGruppe(g.Key.Thema + " · " + g.Key.Gruppe, g.ToArray(), _settings, Sucht));
    public IEnumerable<ObjektListenAnzeige> Listen => FieldCatalog.Objektfelder.Unterlisten
        .Where(l => l.Art == Auswahl.Art)
        .Select(l => new ObjektListenAnzeige(_bearbeitung, Auswahl, l, akte => Auswahl = akte, _darfSchreiben, Neu))
        .Where(l => string.IsNullOrWhiteSpace(Suche) || (l.Titel + " " + l.Inhalt).Contains(Suche, StringComparison.CurrentCultureIgnoreCase));
    public string Quellen => string.Join("\n\n", Auswahl.Quellen.Select(q =>
        $"{q.Modell} · {q.Klasse} · {q.Kennung}\n{q.Datei}\n" +
        string.Join("\n", q.Werte.Select(w => w.Key + ": " + w.Value).Concat(q.Referenzen.Select(r => r.Key + " → " + r.Value)))));

    private bool Passt(ObjektFeldViewModel f)
    {
        if (!string.IsNullOrWhiteSpace(Suche))
            return Suche.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(w =>
                (f.Label + " " + f.Text + " " + f.Feld.Gruppe + " " + f.Feld.Speicherfeld + " " + f.Hinweis).Contains(w, StringComparison.CurrentCultureIgnoreCase));
        return (Anpassen || f.Sichtbar) && (Thema == "Alle Felder" || Thema == "Meine Übersicht" && f.Favorit || Thema == f.Feld.Thema);
    }
    private void Neu(string art)
    {
        if (!_darfSchreiben()) return;
        var neu = _bearbeitung.Neu(art);
        OnPropertyChanged(nameof(Objekte));
        Auswahl = neu;
        _geaendert();
    }
    private void LadeFelder()
    {
        _auswahl = _bearbeitung.Verbund.First(a => a.Id == _auswahl.Id);
        _felder = ObjektaktenBestandsfelder.Fuer(_bearbeitung, Auswahl)
            .Select(f => new ObjektFeldViewModel(_bearbeitung, Auswahl, f, _settings, FeldGeaendert,
                text => Meldung = text, _darfSchreiben, _listeBearbeiten)).ToArray();
        if (!Themen.Contains(Thema)) _thema = "Alle Felder";
        OnPropertyChanged(nameof(Themen)); OnPropertyChanged(nameof(Thema)); OnPropertyChanged(nameof(Gruppen));
        OnPropertyChanged(nameof(Listen)); OnPropertyChanged(nameof(Quellen)); OnPropertyChanged(nameof(Tiefe));
    }
    private void FeldGeaendert()
    {
        _geaendert(); OnPropertyChanged(nameof(Tiefe)); OnPropertyChanged(nameof(Listen));
        foreach (var feld in _felder.Where(f => f.Feld.Elternfeld is not null || SchachtHoehenRechnung.IstHoehenfeld(f.Feld.Id)
            || f.Feld.Id == "schacht.objectid"))
            feld.AktualisiereAuswahl();
        Meldung = "Änderung übernommen. Bitte das Projekt speichern.";
    }
    public void SpeichereAnsicht() => _settings.Save();
    public void AktualisiereFelder() { if (_darfSchreiben()) LadeFelder(); }
}

public sealed class ObjektFeldGruppe(string titel, IReadOnlyList<ObjektFeldViewModel> felder, AppSettings settings, bool suche = false)
{
    public string Titel => titel;
    public IReadOnlyList<ObjektFeldViewModel> Felder => felder;
    public bool Offen { get => suche || !settings.ObjektakteGruppen.TryGetValue(titel, out var offen) || offen; set { if (!suche) settings.ObjektakteGruppen[titel] = value; } }
}

public sealed record ObjektWahl(ObjektAkte Akte, string Titel);
