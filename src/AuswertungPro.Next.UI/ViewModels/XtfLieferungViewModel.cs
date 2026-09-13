using System.Collections.ObjectModel;
using System.IO;
using AuswertungPro.Next.Application.Xtf.Lieferung;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels;

public sealed class XtfLieferungViewModel : ObservableObject
{
    private readonly IXtfLieferungsAblage _ablage;
    private readonly IDialogService _dialogs;
    private CancellationTokenSource? _abbruch;
    private XtfLieferungsInfo? _info;
    private XtfLieferungsObjekt? _objekt;
    private XtfLieferungsZeile? _auswahl;
    private string _suche = "", _klasse = "Alle Objektarten", _status = "Eine Original-XTF oder bestehende Arbeitsdatei öffnen.", _bericht = "";
    private bool _arbeitet, _nurProbleme;
    private int _seite;
    private long _treffer;
    public ObservableCollection<XtfLieferungsZeile> Zeilen { get; } = [];
    public ObservableCollection<XtfLieferungsFeldViewModel> Felder { get; } = [];
    public IReadOnlyList<string> Klassen => new[] { "Alle Objektarten" }.Concat(_info?.Gruppen.Select(g => g.Klasse) ?? []).ToArray();
    public string DateiZeile => _info is null ? "Eigene Arbeitsdatei; keine Änderung am Original." : $"{_info.Anzahl:N0} Objekte · {_info.Geaendert:N0} bearbeitet · {_info.Datei}";
    public string ObjektZeile => _objekt is null ? "Objekt links auswählen" : $"{_objekt.Klasse} · Originalkennung: {(_objekt.Kennung.Length == 0 ? "Beziehung ohne eigene Kennung" : _objekt.Kennung)}";
    public string SeitenZeile => $"Seite {_seite + 1} von {Math.Max(1, (_treffer + 99) / 100)} · {_treffer:N0} Treffer";
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string BerichtVorschau => _bericht.Length > 6000 ? _bericht[..6000] + "\n… Vollständigen Bericht über «Bericht speichern» öffnen." : _bericht;
    public bool HatEntwurf => Felder.Any(f => f.Geaendert);
    public bool Arbeitet { get => _arbeitet; private set { SetProperty(ref _arbeitet, value); OnPropertyChanged(nameof(Bereit)); BefehleAktualisieren(); } }
    public bool Bereit => !Arbeitet;
    public string Suche { get => _suche; set => SetProperty(ref _suche, value); }
    public string Klasse { get => _klasse; set => SetProperty(ref _klasse, value); }
    public bool NurProbleme { get => _nurProbleme; set => SetProperty(ref _nurProbleme, value); }
    public Task AuswahlLaden { get; private set; } = Task.CompletedTask;
    public XtfLieferungsZeile? Auswahl
    {
        get => _auswahl;
        set
        {
            if (_auswahl == value) return;
            if (!WechselErlaubt()) { OnPropertyChanged(); return; }
            SetProperty(ref _auswahl, value);
            if (value is not null) AuswahlLaden = Lauf(async token =>
            {
                var o = await Task.Run(() => _ablage.Lies(_info!.Datei, value.Id), token); ZeigeObjekt(o);
            });
        }
    }
    public IAsyncRelayCommand OeffnenCommand { get; }
    public IAsyncRelayCommand SuchenCommand { get; }
    public IAsyncRelayCommand VorherigeCommand { get; }
    public IAsyncRelayCommand NaechsteCommand { get; }
    public IAsyncRelayCommand SpeichernCommand { get; }
    public IRelayCommand VerwerfenCommand { get; }
    public IAsyncRelayCommand PruefenCommand { get; }
    public IAsyncRelayCommand ExportierenCommand { get; }
    public IAsyncRelayCommand BerichtSpeichernCommand { get; }
    public IRelayCommand AbbrechenCommand { get; }

    public XtfLieferungViewModel(IXtfLieferungsAblage ablage, IDialogService dialogs)
    {
        _ablage = ablage; _dialogs = dialogs;
        OeffnenCommand = new AsyncRelayCommand(Oeffnen, () => Bereit);
        SuchenCommand = new AsyncRelayCommand(() => SucheSeite(0), () => Bereit && _info is not null);
        VorherigeCommand = new AsyncRelayCommand(() => SucheSeite(_seite - 1), () => Bereit && _seite > 0);
        NaechsteCommand = new AsyncRelayCommand(() => SucheSeite(_seite + 1), () => Bereit && (_seite + 1L) * 100 < _treffer);
        SpeichernCommand = new AsyncRelayCommand(Speichern, () => Bereit && HatEntwurf);
        VerwerfenCommand = new RelayCommand(() => { if (_objekt is not null) ZeigeObjekt(_objekt); }, () => Bereit && HatEntwurf);
        PruefenCommand = new AsyncRelayCommand(() => Pruefen(false), () => Bereit && _info is not null);
        ExportierenCommand = new AsyncRelayCommand(() => Pruefen(true), () => Bereit && _info is not null);
        BerichtSpeichernCommand = new AsyncRelayCommand(BerichtSpeichern, () => Bereit && _info is not null && _bericht.Length > 0);
        AbbrechenCommand = new RelayCommand(() => _abbruch?.Cancel(), () => Arbeitet);
    }
    private bool WechselErlaubt()
    {
        if (Arbeitet) return false;
        if (!HatEntwurf) return true;
        Status = "Bitte die Eingaben zuerst speichern oder verwerfen."; return false;
    }
    public bool DarfSchliessen() => WechselErlaubt();
    private async Task Oeffnen()
    {
        if (!WechselErlaubt()) return;
        var quelle = _dialogs.OpenFile("Lieferung öffnen", "XTF oder Arbeitsdatei|*.xtf;*.ssxtf"); if (quelle is null) return;
        var import = !Path.GetExtension(quelle).Equals(".ssxtf", StringComparison.OrdinalIgnoreCase);
        var arbeit = import ? _dialogs.SaveFile("Neue Arbeitsdatei speichern", "SewerStudio-Lieferung|*.ssxtf", ".ssxtf", Path.GetFileNameWithoutExtension(quelle) + ".ssxtf") : quelle;
        if (arbeit is null) return;
        await Lauf(async token =>
        {
            var fortschritt = new Progress<string>(s => Status = s);
            _info = await Task.Run(() => import ? _ablage.Importiere(quelle, arbeit, fortschritt, token) : _ablage.Oeffne(arbeit), token);
            _seite = 0; Klasse = "Alle Objektarten"; Suche = ""; NurProbleme = false; _bericht = "";
            OnPropertyChanged(nameof(DateiZeile)); OnPropertyChanged(nameof(Klassen)); OnPropertyChanged(nameof(BerichtVorschau));
            await LadeSeite(token); Status = "Arbeitsdatei geöffnet. Originalkennungen bleiben erhalten.";
        });
    }
    private async Task SucheSeite(int seite)
    {
        if (!WechselErlaubt() || _info is null) return;
        await Lauf(async token => { _seite = seite; await LadeSeite(token); });
    }
    private async Task LadeSeite(CancellationToken token)
    {
        var seite = await Task.Run(() => _ablage.Suche(_info!.Datei, Klasse == "Alle Objektarten" ? null : Klasse, Suche, _seite, NurProbleme), token);
        _auswahl = null; _objekt = null; Felder.Clear(); Zeilen.Clear(); _treffer = seite.Gesamt;
        foreach (var z in seite.Zeilen) Zeilen.Add(z);
        OnPropertyChanged(nameof(Auswahl)); OnPropertyChanged(nameof(ObjektZeile)); OnPropertyChanged(nameof(SeitenZeile)); EntwurfGeaendert();
    }
    private void ZeigeObjekt(XtfLieferungsObjekt o)
    {
        _objekt = o; Felder.Clear();
        foreach (var f in o.Felder) Felder.Add(new(f, EntwurfGeaendert));
        OnPropertyChanged(nameof(ObjektZeile)); EntwurfGeaendert();
    }
    private async Task Speichern()
    {
        if (_objekt is null || _info is null) return;
        var o = _objekt; var patch = Felder.Where(f => f.Geaendert).ToDictionary(f => f.Feld.Schluessel, f => f.Wert);
        await Lauf(async token =>
        {
            await Task.Run(() => _ablage.Speichere(_info.Datei, o.Id, o.Version, patch), token);
            ZeigeObjekt(await Task.Run(() => _ablage.Lies(_info.Datei, o.Id), token));
            if (_auswahl is { } vorher && Zeilen.IndexOf(vorher) is var index && index >= 0)
            {
                var name = _objekt!.Felder.FirstOrDefault(f => f.Schluessel == "Bezeichnung")?.Wert
                    ?? _objekt.Felder.FirstOrDefault(f => f.Schluessel == "Textinhalt")?.Wert ?? "";
                _auswahl = vorher with { Bezeichnung = name, Geaendert = true, Problem = "" };
                Zeilen[index] = _auswahl; OnPropertyChanged(nameof(Auswahl));
            }
            _info = await Task.Run(() => _ablage.Oeffne(_info.Datei), token);
            OnPropertyChanged(nameof(DateiZeile)); _bericht = ""; OnPropertyChanged(nameof(BerichtVorschau));
            Status = "Änderung in der Arbeitsdatei gespeichert. Vor der Ausgabe erneut prüfen.";
        });
    }
    private async Task Pruefen(bool export)
    {
        if (!WechselErlaubt() || _info is null) return;
        var ziel = export ? _dialogs.SaveFile("Neue XTF schreiben", "INTERLIS-Lieferung|*.xtf", ".xtf", "Lieferung_neu.xtf") : null;
        if (export && ziel is null) return;
        await Lauf(async token =>
        {
            var fortschritt = new Progress<string>(s => Status = s);
            var r = await Task.Run(() => export ? _ablage.Exportiere(_info.Datei, ziel!, fortschritt, token) : _ablage.Pruefe(_info.Datei, fortschritt, token), token);
            _bericht = r.Bericht; OnPropertyChanged(nameof(BerichtVorschau));
            await LadeSeite(token);
            Status = r.FehlerhafteObjekte > 0 ? $"{r.FehlerhafteObjekte:N0} Objekte benötigen Korrekturen. Keine XTF geschrieben."
                : export ? "Neue XTF geschrieben. Hinweise im Prüfbericht beachten." : "Objektprüfung bestanden. Hinweise im Prüfbericht beachten.";
        });
    }
    private async Task BerichtSpeichern()
    {
        var ziel = _dialogs.SaveFile("Vollständigen Prüfbericht speichern", "Textdatei|*.txt", ".txt", "Lieferung_Pruefbericht.txt");
        if (ziel is not null) await Lauf(token => Task.Run(() => _ablage.SichereBericht(_info!.Datei, ziel), token));
    }
    private async Task Lauf(Func<CancellationToken, Task> arbeit)
    {
        if (Arbeitet) return;
        using var abbruch = new CancellationTokenSource(); _abbruch = abbruch; Arbeitet = true;
        try { await arbeit(abbruch.Token); }
        catch (OperationCanceledException) { Status = "Vorgang abgebrochen. Originaldatei bleibt erhalten."; }
        catch (Exception e) { Status = "Vorgang nicht abgeschlossen: " + e.Message; }
        finally { _abbruch = null; Arbeitet = false; }
    }
    private void EntwurfGeaendert() { OnPropertyChanged(nameof(HatEntwurf)); BefehleAktualisieren(); }
    private void BefehleAktualisieren()
    {
        foreach (var c in new IRelayCommand?[] { OeffnenCommand, SuchenCommand, VorherigeCommand, NaechsteCommand, SpeichernCommand,
            VerwerfenCommand, PruefenCommand, ExportierenCommand, BerichtSpeichernCommand, AbbrechenCommand }) c?.NotifyCanExecuteChanged();
    }
}

public sealed class XtfLieferungsFeldViewModel(XtfLieferungsFeld feld, Action geaendert) : ObservableObject
{
    private string _wert = feld.Wert;
    public XtfLieferungsFeld Feld => feld;
    public string Label => feld.Label + (feld.Pflicht ? " *" : "");
    public bool HatAuswahl => feld.Typ == "Enum";
    public bool NurLesen => !feld.Bearbeitbar;
    public string Hinweis => HatAuswahl && Wert.Length > 0 && !feld.Optionen.Contains(Wert)
        ? "Wert ausserhalb der Normliste: " + Wert : feld.Hinweis;
    public bool Geaendert => Wert != feld.Wert;
    public string Wert { get => _wert; set { if (SetProperty(ref _wert, value ?? "")) { OnPropertyChanged(nameof(Geaendert)); OnPropertyChanged(nameof(Hinweis)); geaendert(); } } }
}
