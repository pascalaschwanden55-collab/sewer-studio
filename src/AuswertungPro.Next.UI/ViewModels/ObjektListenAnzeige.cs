using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels;

/// <summary>Eine Aufklappliste der Objektakte als Tabelle.
///
/// Zeilen kommen aus zwei Quellen: eigene Akten sind bearbeitbar und oeffnen dieselbe
/// Maske wie Deckel und Sanierung; Zeilen aus dem Katasterabgleich bleiben lesend, bis
/// sie jemand bewusst uebernimmt. Ein eigener Editor entsteht hier nicht.
/// </summary>
public sealed class ObjektListenAnzeige : ObservableObject
{
    private readonly ObjektUnterliste _liste;

    public ObjektListenAnzeige(ObjektaktenBearbeitung bearbeitung, ObjektAkte wurzel, ObjektUnterliste liste,
        Action<ObjektAkte> oeffnen, Func<bool> darfSchreiben, Action<string> anlegen)
    {
        _liste = liste;
        Spaltentitel = ObjektaktenListen.Spalten(liste);
        var felder = Spaltentitel.Select(s => ObjektaktenListen.Spaltenfeld(liste, s)).ToArray();

        var zeilen = new List<ObjektListenZeile>();
        foreach (var akte in ObjektaktenListen.Akten(bearbeitung, wurzel, liste))
            zeilen.Add(new ObjektListenZeile(akte,
                felder.Select(f => f is null ? "" : bearbeitung.Lies(akte, f)).ToArray(), ""));
        foreach (var zeile in ObjektaktenListen.Zeilen(bearbeitung, wurzel, liste))
            zeilen.Add(new ObjektListenZeile(null,
                Spaltentitel.Select(s => zeile.GetValueOrDefault(s, "")).ToArray(),
                Herkunft(zeile)));
        Zeilen = zeilen;

        DarfAnlegen = liste.DarfAnlegen && darfSchreiben();
        NeuCommand = new RelayCommand(() => anlegen(liste.ZeigtAufObjektart), () => DarfAnlegen);
        OeffnenCommand = new RelayCommand<ObjektListenZeile>(
            z => { if (z?.Akte is { } akte) oeffnen(akte); },
            z => z?.Akte is not null);
    }

    public string Titel => _liste.Label;
    public IReadOnlyList<string> Spaltentitel { get; }
    public IReadOnlyList<ObjektListenZeile> Zeilen { get; }
    public bool DarfAnlegen { get; }
    public bool NurLesen => _liste.NurLesen;
    public IRelayCommand NeuCommand { get; }
    public IRelayCommand<ObjektListenZeile> OeffnenCommand { get; }

    /// <summary>Spaltenkopf als eine Zeile - fuer schmale Darstellungen.</summary>
    public string Spalten => string.Join(" · ", Spaltentitel);

    public bool HatZeilen => Zeilen.Count > 0;

    public string Hinweis => Zeilen.Count > 0 ? ""
        : _liste.NurLesen
            ? "Keine Einträge. Diese Liste wird am zugehörigen Objekt gepflegt."
            : DarfAnlegen ? "Noch keine Einträge." : "Keine Einträge.";

    /// <summary>Kompatibilitaet fuer Ansichten, die den alten Fliesstext lesen.</summary>
    public string Inhalt => Zeilen.Count == 0 ? Hinweis
        : string.Join("\n", Zeilen.Select(z => z.Anzeige));

    private static string Herkunft(IReadOnlyDictionary<string, string> zeile)
        => zeile.Count == 0 ? "" : "aus dem Katasterabgleich";
}

/// <summary>Eine Zeile. <see cref="Akte"/> ist leer, wenn die Zeile aus einer Quelle stammt
/// und hier nicht bearbeitet werden kann.</summary>
public sealed record ObjektListenZeile(ObjektAkte? Akte, IReadOnlyList<string> Werte, string Herkunft)
{
    public bool Bearbeitbar => Akte is not null;

    public string Anzeige
    {
        get
        {
            var sichtbar = Werte.Where(w => !string.IsNullOrWhiteSpace(w)).ToArray();
            var text = sichtbar.Length > 0 ? string.Join(" · ", sichtbar) : "(ohne Angaben)";
            return Herkunft.Length > 0 ? $"{text} — {Herkunft}" : text;
        }
    }
}
