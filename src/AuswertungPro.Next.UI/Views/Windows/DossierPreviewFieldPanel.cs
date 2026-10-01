using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Die Eingabeseite der Vorschau: alle Felder zur gerade gewählten Seite.
///
/// Herausgelöst aus <see cref="DossierPreviewWindow"/>. Das Fenster hatte mit
/// 1'999 von 2'000 erlaubten Zeilen die Grenze erreicht, und der Feldbereich
/// war mit rund drei Vierteln davon der grösste Teil darin. Er ist auch die
/// eigenständigere Aufgabe: Das Fenster zeichnet das Blatt und führt die
/// Seitenliste; hier entstehen die Eingaben dazu.
///
/// Was diese Klasse vom Fenster braucht, steht ausdrücklich im Konstruktor —
/// kein Zugriff zurück auf das Fenster. Zwei Angaben sind bewusst Funktionen
/// und keine Werte: Die Werte der Vorlage entstehen bei jedem Neuzeichnen neu,
/// und die Ressourcen hängen am geladenen Fenster.
/// </summary>
internal sealed partial class DossierPreviewFieldPanel
{
    private readonly Panel _wirt;
    private readonly DossierAreaSettings _area;
    private readonly DossierDefinition _dossier;
    private readonly string _planWorkFolder;
    private readonly DossierPreviewDocument _document;
    private readonly IPlanImageConverter _planImages;
    private readonly IPlanImageAdjuster _planAdjuster;
    private readonly DossierTextUndoController _textUndo;

    /// <summary>Die Werte der Vorlage — bei jedem Neuzeichnen ein neuer Stand.</summary>
    private readonly Func<IReadOnlyDictionary<string, string>> _werte;

    private readonly Action _zeichneBlatt;
    private readonly Action<DossierPreviewTarget> _betoneZiel;
    private readonly Action<DossierPreviewTarget, bool> _hervorheben;
    private readonly Func<object, object> _ressource;

    /// <summary>Meldung in der Statuszeile des Fensters.</summary>
    private readonly Action<string> _status;

    /// <summary>Besitzerfenster fuer Dateidialoge.</summary>
    private readonly Func<Window> _fenster;

    public DossierPreviewFieldPanel(
        Panel wirt,
        DossierAreaSettings area,
        DossierDefinition dossier,
        string planWorkFolder,
        DossierPreviewDocument document,
        IPlanImageConverter planImages,
        IPlanImageAdjuster planAdjuster,
        Func<IReadOnlyDictionary<string, string>> werte,
        Action zeichneBlatt,
        Action<DossierPreviewTarget> betone,
        Action<DossierPreviewTarget, bool> hervorheben,
        Func<object, object> ressource,
        Action<string> status,
        Func<Window> fenster)
    {
        _wirt = wirt ?? throw new ArgumentNullException(nameof(wirt));
        _textUndo = new DossierTextUndoController(_wirt);
        _area = area ?? throw new ArgumentNullException(nameof(area));
        _dossier = dossier ?? throw new ArgumentNullException(nameof(dossier));
        _planWorkFolder = !string.IsNullOrWhiteSpace(planWorkFolder)
            ? planWorkFolder
            : throw new ArgumentException("Der Plan-Arbeitsordner fehlt.", nameof(planWorkFolder));
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _planImages = planImages ?? throw new ArgumentNullException(nameof(planImages));
        _planAdjuster = planAdjuster ?? throw new ArgumentNullException(nameof(planAdjuster));
        _werte = werte ?? throw new ArgumentNullException(nameof(werte));
        _zeichneBlatt = zeichneBlatt ?? throw new ArgumentNullException(nameof(zeichneBlatt));
        _betoneZiel = betone ?? throw new ArgumentNullException(nameof(betone));
        _hervorheben = hervorheben ?? throw new ArgumentNullException(nameof(hervorheben));
        _ressource = ressource ?? throw new ArgumentNullException(nameof(ressource));
        _status = status ?? throw new ArgumentNullException(nameof(status));
        _fenster = fenster ?? throw new ArgumentNullException(nameof(fenster));
    }

    /// <summary>
    /// Merkt die bearbeitete Stelle und laesst sie aufblinken. Zwei Formen,
    /// weil die meisten Aufrufer nur einen Feldschluessel zur Hand haben.
    /// </summary>
    private void Betone(DossierPreviewTarget target) => _betoneZiel(target);

    private void Betone(string fieldKey) => _betoneZiel(DossierPreviewTarget.Field(fieldKey));

    /// <summary>Alle Ziele, zu denen es eine Eingabestelle gibt.</summary>
    public IReadOnlyCollection<DossierPreviewTarget> Ziele => _feldStellen.Keys;

    /// <summary>Kennt die Eingabeseite eine Stelle zu diesem Ziel?</summary>
    public bool Kennt(DossierPreviewTarget target) => _feldStellen.ContainsKey(target);

    /// <summary>Baut die Felder zur gewählten Seite neu auf.</summary>
    public void Baue(DossierPreviewPage seite, IReadOnlyList<DossierPreviewField> felder)
        => BaueFelder([(seite, felder)]);

    /// <summary>
    /// Baut die Felder zu allen Kapiteln des gewählten Blattes neu auf.
    ///
    /// Ein Ausgabeblatt trägt oft mehr als ein Kapitel — ohne gewählten
    /// Übersichtsplan rutscht „Eigentumsverhältnisse" auf dasselbe Blatt.
    /// Zeigte die Eingabeseite dann nur eines davon, wären die Felder des
    /// anderen unerreichbar, darunter die Auswahl des Plans selbst.
    /// </summary>
    public void Baue(
        IReadOnlyList<(DossierPreviewPage Seite, IReadOnlyList<DossierPreviewField> Felder)> seiten)
        => BaueFelder(seiten);

    /// <summary>
    /// Springt zu der Stelle, die im Blatt angeklickt wurde. Falsch, wenn es
    /// dafür kein Eingabefeld gibt — dann soll der Klick nichts vortäuschen.
    /// </summary>
    public bool SpringeZu(DossierPreviewTarget target) => SpringeZuFeld(target);
}
