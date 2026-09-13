using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuswertungPro.Next.UI.ViewModels;

public sealed class ObjektFeldViewModel : ObservableObject
{
    private readonly ObjektaktenBearbeitung _bearbeitung;
    private ObjektAkte _akte;
    private readonly Action _geaendert;
    private readonly Action<string> _fehler;
    private readonly Func<bool> _darfSchreiben;
    private readonly AppSettings _settings;
    private string _erwartet;
    private string _text;
    private string _auswahlsuche = "";
    private string _hinweis = "";
    public ObjektFeldDefinition Feld { get; }
    public string Label => Feld.Label;
    public bool Pflicht => Feld.WebgisPflicht;
    public string PflichtLabel => Feld.Label + (Pflicht ? " *" : "");
    public bool Bearbeitbar => !Feld.NurLesen;
    public bool HatAuswahl => Feld.KatalogId is not null;
    public bool HatBearbeitbareAuswahl => HatAuswahl && Bearbeitbar;
    public string Herkunft => $"{Feld.Exportziel ?? "Zusatzangabe"} · Zuordnung: {Feld.Belegstatus}";
    public string Hinweis { get => _hinweis; private set => SetProperty(ref _hinweis, value); }

    /// <summary>Rechtsklick «Liste bearbeiten…»: eigene Eintraege, Umbenennen, Ausblenden - programmweit.</summary>
    public bool KannListeBearbeiten { get; }
    public CommunityToolkit.Mvvm.Input.IRelayCommand ListeBearbeitenCommand { get; }
    public ObjektaktenBearbeitung Bearbeitung => _bearbeitung;
    public ObjektAkte Akte => _akte;

    public ObjektFeldViewModel(ObjektaktenBearbeitung bearbeitung, ObjektAkte akte, ObjektFeldDefinition feld,
        AppSettings settings, Action geaendert, Action<string> fehler, Func<bool> darfSchreiben,
        Action<ObjektFeldViewModel>? listeBearbeiten = null)
    {
        _bearbeitung = bearbeitung; _akte = akte; Feld = feld; _settings = settings;
        _geaendert = geaendert; _fehler = fehler; _darfSchreiben = darfSchreiben;
        // «Liste bearbeiten» gibt es nur, wenn ein Speicher fuer Ergaenzungen angeschlossen ist
        // und das Feld eine bearbeitbare Auswahl hat.
        KannListeBearbeiten = listeBearbeiten is not null && HatBearbeitbareAuswahl && bearbeitung.ErgaenzungenSpeicher is not null;
        ListeBearbeitenCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(
            () => listeBearbeiten?.Invoke(this), () => KannListeBearbeiten && _darfSchreiben());
        _erwartet = bearbeitung.Lies(akte, feld);
        _text = _erwartet;
        if (akte.Werte.TryGetValue(feld.Id, out var wert) && wert.Bestandswert == _erwartet)
            _text = wert.Text;
        AktualisiereAuswahl();
    }

    public string Text
    {
        get => _text;
        set { if (value != _text) Schreibe(value ?? "", null); }
    }

    public IEnumerable<ObjektAuswahl> Optionen => ErlaubteOptionen().Where(e =>
        string.IsNullOrWhiteSpace(_auswahlsuche) || e.Label.Contains(_auswahlsuche, StringComparison.CurrentCultureIgnoreCase)
        || (e.OriginalCode?.Contains(_auswahlsuche, StringComparison.OrdinalIgnoreCase) ?? false));

    public string Auswahlsuche
    {
        get => _auswahlsuche;
        set { if (SetProperty(ref _auswahlsuche, value)) OnPropertyChanged(nameof(Optionen)); }
    }

    public ObjektAuswahl? Auswahl
    {
        get
        {
            // Ein gespeicherter Wert kann aus dem Katalog des Feldes oder aus dem Katalog je
            // Elternwert stammen; der Abgleich laeuft ueber Position und Originalcode.
            if (_akte.Werte.TryGetValue(Feld.Id, out var w) && w.KatalogId is not null
                && (w.KatalogId == Feld.KatalogId || w.KatalogId == Feld.KatalogIdJeEltern)
                && (w.Bestandswert is null || w.Bestandswert == _erwartet))
            {
                var optionen = ErlaubteOptionen().ToArray();
                // Die ersten Erhebungen hatten noch keine Originalcodes. Nach deren Ergänzung
                // bleibt die alte Position zusammen mit dem Text eindeutig, auch bei doppelten
                // Anzeigetexten. Nur beim Lesen zuordnen; gespeicherte Werte nicht umschreiben.
                if (w.Originalcode is null)
                    return optionen.SingleOrDefault(e => e.Index == w.LokalerEintrag && e.Label == w.Text);
                return optionen.FirstOrDefault(e => e.Index == w.LokalerEintrag && e.OriginalCode == w.Originalcode)
                    ?? optionen.SingleOrDefault(e => e.OriginalCode == w.Originalcode && e.Label == w.Text);
            }
            var treffer = ErlaubteOptionen().Where(e => e.Label == Text).Take(2).ToArray();
            return treffer.Length == 1 ? treffer[0] : null;
        }
        set { if (value is not null && value != Auswahl) Schreibe(value.Label, value); }
    }

    public bool Sichtbar
    {
        get => !_settings.ObjektakteSichtbarkeit.TryGetValue(Feld.Id, out var sichtbar) || sichtbar;
        set { _settings.ObjektakteSichtbarkeit[Feld.Id] = value; OnPropertyChanged(); }
    }
    public bool Favorit
    {
        get => _settings.ObjektakteFavoriten.GetValueOrDefault(Feld.Id);
        set { _settings.ObjektakteFavoriten[Feld.Id] = value; OnPropertyChanged(); }
    }

    /// <summary>Feldmarkierung «will ich ausfuellen» (11.09.2026): Rechtsklick, eine der fuenf Farben,
    /// programmweit je Feld gespeichert - in jedem Projekt dieselben markierten Felder. Leer heisst keine.</summary>
    public static readonly IReadOnlyList<string> Farben = ["Gelb", "Orange", "Rot", "Grün", "Blau"];
    public string Farbe
    {
        get => _settings.ObjektakteFarben.GetValueOrDefault(Feld.Id, "");
        set
        {
            var farbe = value ?? "";
            if (farbe.Length > 0 && !Farben.Contains(farbe)) throw new ArgumentException($"Unbekannte Markierungsfarbe «{farbe}».", nameof(value));
            if (farbe.Length == 0) _settings.ObjektakteFarben.Remove(Feld.Id);
            else _settings.ObjektakteFarben[Feld.Id] = farbe;
            OnPropertyChanged();
        }
    }
    public CommunityToolkit.Mvvm.Input.IRelayCommand<string> FarbeCommand => new CommunityToolkit.Mvvm.Input.RelayCommand<string>(f => Farbe = f ?? "");

    // Welche Eintraege gerade gelten (Katalog, Elterngruppe, eigene Ergaenzungen), entscheidet
    // die Bearbeitung - dieselbe Regel, gegen die auch das Schreiben prueft.
    private IEnumerable<ObjektAuswahl> ErlaubteOptionen() => _bearbeitung.ErlaubteEintraege(_akte, Feld);

    private string ElternHinweis()
    {
        if (Feld.Elternfeld is not { } parent || ErlaubteOptionen().Any()) return "";
        var eltern = FieldCatalog.Objektfelder.Feld(parent);
        if (Feld.KatalogIdJeEltern is null)
            return "Diese Unterliste ist noch nicht belegt. Der vorhandene Wert bleibt erhalten.";
        var elterntext = _bearbeitung.Lies(_akte, eltern);
        return elterntext.Length == 0
            ? $"Zuerst «{eltern.Label}» wählen."
            : $"Für «{elterntext}» gibt es keine Unterliste. Der vorhandene Wert bleibt erhalten.";
    }

    public void AktualisiereAuswahl()
    {
        LiesWertNeu();
        var elternHinweis = ElternHinweis();
        Hinweis = elternHinweis.Length > 0 ? elternHinweis
            : HatAuswahl && Text.Length > 0 && !ErlaubteOptionen().Any(e => e.Label == Text)
                ? "Vorhandener Wert ausserhalb der belegten Auswahl; bleibt erhalten."
                : "";
        if (_akte.Werte.TryGetValue(Feld.Id, out var roh) && roh.Originalcode is { Length: > 0 } code)
            Hinweis = (Hinweis + " Originalcode: " + code).Trim();
        if (AuswertungPro.Next.Application.Xtf.Dss.DssMaterialZuordnung.Fuer(Feld.Id, Text) is { } material)
            Hinweis = (Hinweis + " " + (material.Normwert is null ? material.Hinweis : $"DSS-Material: {material.Normwert}. {material.Hinweis}")).Trim();
        OnPropertyChanged(nameof(Optionen)); OnPropertyChanged(nameof(Auswahl));
    }

    /// <summary>Holt den angezeigten Wert aus dem Modell zurueck. Noetig, weil ein abhaengiges Feld
    /// nicht nur von Hand, sondern auch durch einen Wechsel seines Elternwerts gesetzt wird
    /// (Materialgruppe -> Materialdetail). Schreibt nie - der Text-Setter bleibt aussen vor.</summary>
    private void LiesWertNeu()
    {
        _akte = _bearbeitung.Projekt.Objektakten.SingleOrDefault(a => a.Id == _akte.Id) ?? _akte;
        _erwartet = _bearbeitung.Lies(_akte, Feld);
        var text = _erwartet;
        if (_akte.Werte.TryGetValue(Feld.Id, out var wert) && wert.Bestandswert == _erwartet)
            text = wert.Text;
        SetProperty(ref _text, text, nameof(Text));
    }

    private void Schreibe(string text, ObjektAuswahl? auswahl)
    {
        try
        {
            if (!_darfSchreiben()) throw new InvalidOperationException("Das Projekt ist derzeit nicht bearbeitbar.");
            _bearbeitung.Schreibe(_akte, Feld, _erwartet, text, auswahl);
            _akte = _bearbeitung.Projekt.Objektakten.Single(a => a.Id == _akte.Id);
            _erwartet = _bearbeitung.Lies(_akte, Feld);
            SetProperty(ref _text, text, nameof(Text));
            _geaendert(); AktualisiereAuswahl();
        }
        catch (InvalidOperationException ex) { _fehler(ex.Message); OnPropertyChanged(nameof(Text)); }
    }
}
