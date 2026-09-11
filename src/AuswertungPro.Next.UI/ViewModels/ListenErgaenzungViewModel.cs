using System;
using System.Collections.ObjectModel;
using System.Linq;
using AuswertungPro.Next.Application.UseCases.Objektakten;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels;

/// <summary>Fenster «Liste bearbeiten»: duenne Huelle um <see cref="ListenErgaenzungBearbeitung"/>.
/// Die Regeln (kein Doppeleintrag, WebGIS-Eintraege nur ausblenden) liegen dort, nicht hier.</summary>
public sealed class ListenErgaenzungViewModel : ObservableObject
{
    private readonly ListenErgaenzungBearbeitung _bearbeitung;
    private string _neuText = "";
    private string _neuCode = "";
    private string _fehler = "";

    public ListenErgaenzungViewModel(ListenErgaenzungBearbeitung bearbeitung)
    {
        _bearbeitung = bearbeitung;
        Zeilen = new ObservableCollection<ListenErgaenzungZeileViewModel>(
            bearbeitung.Zeilen.Select(z => new ListenErgaenzungZeileViewModel(z, this)));
        HinzufuegenCommand = new RelayCommand(Hinzufuegen, () => !string.IsNullOrWhiteSpace(NeuText));
        SpeichernCommand = new RelayCommand(Speichern);
        AbbrechenCommand = new RelayCommand(() => Schliessen?.Invoke());
    }

    public string Titel => _bearbeitung.Titel;
    public string Untertitel => _bearbeitung.Eltern is null
        ? "Änderungen gelten in allen Projekten. Der WebGIS-Katalog bleibt unverändert."
        : "Nur für diese Elterngruppe. Änderungen gelten in allen Projekten; der WebGIS-Katalog bleibt unverändert.";
    public ObservableCollection<ListenErgaenzungZeileViewModel> Zeilen { get; }
    public IRelayCommand HinzufuegenCommand { get; }
    public IRelayCommand SpeichernCommand { get; }
    public IRelayCommand AbbrechenCommand { get; }
    public Action? Schliessen { get; set; }
    public bool Gespeichert { get; private set; }

    public string NeuText
    {
        get => _neuText;
        set { if (SetProperty(ref _neuText, value ?? "")) HinzufuegenCommand.NotifyCanExecuteChanged(); }
    }
    public string NeuCode { get => _neuCode; set => SetProperty(ref _neuCode, value ?? ""); }
    public string Fehler { get => _fehler; private set => SetProperty(ref _fehler, value); }

    internal void Melde(Action aktion)
    {
        try { aktion(); Fehler = ""; }
        catch (ArgumentException ex) { Fehler = ex.Message; }
        catch (InvalidOperationException ex) { Fehler = ex.Message; }
    }

    internal void Entferne(ListenErgaenzungZeileViewModel zeile)
        => Melde(() => { _bearbeitung.Entfernen(zeile.Zeile); Zeilen.Remove(zeile); });

    internal void Umbenenne(ListenErgaenzungZeileViewModel zeile, string text)
        => Melde(() => _bearbeitung.Umbenennen(zeile.Zeile.Code!, text));

    internal void BlendeAus(ListenErgaenzungZeileViewModel zeile, bool aus)
        => Melde(() => _bearbeitung.Ausblenden(zeile.Zeile.Code!, aus));

    private void Hinzufuegen() => Melde(() =>
    {
        var neu = _bearbeitung.Hinzufuegen(NeuText, NeuCode);
        Zeilen.Add(new ListenErgaenzungZeileViewModel(neu, this));
        NeuText = ""; NeuCode = "";
    });

    private void Speichern() => Melde(() =>
    {
        _bearbeitung.Speichere();
        Gespeichert = true;
        Schliessen?.Invoke();
    });
}

public sealed class ListenErgaenzungZeileViewModel : ObservableObject
{
    private readonly ListenErgaenzungViewModel _eltern;
    private string _text;
    private bool _ausgeblendet;

    public ListenErgaenzungZeileViewModel(ListenErgaenzungZeile zeile, ListenErgaenzungViewModel eltern)
    {
        Zeile = zeile; _eltern = eltern; _text = zeile.Text; _ausgeblendet = zeile.Ausgeblendet;
        EntfernenCommand = new RelayCommand(() => eltern.Entferne(this), () => zeile.Eigen);
    }

    public ListenErgaenzungZeile Zeile { get; }
    public string? Code => Zeile.Code;
    public string Originaltext => Zeile.Originaltext;
    public bool Eigen => Zeile.Eigen;
    public bool IstWebGis => !Zeile.Eigen;
    public string Herkunft => Zeile.Eigen ? "eigener Eintrag" : $"WebGIS · Code {Zeile.Code}";
    public IRelayCommand EntfernenCommand { get; }

    public string Text
    {
        get => _text;
        set
        {
            var neu = (value ?? "").Trim();
            if (neu == _text || Zeile.Eigen) return;
            _eltern.Umbenenne(this, neu);
            _text = neu.Length == 0 ? _text : neu;
            OnPropertyChanged();
        }
    }

    public bool Ausgeblendet
    {
        get => _ausgeblendet;
        set
        {
            if (value == _ausgeblendet || Zeile.Eigen) return;
            _eltern.BlendeAus(this, value);
            _ausgeblendet = value;
            OnPropertyChanged();
        }
    }
}
