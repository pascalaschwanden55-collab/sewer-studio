using System.Text.Json;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>Anzeigezuordnung aus den Quellbereichen des WebGIS-Plans vom 10.09.2026. Keine Speicherzuordnung.</summary>
public static class ObjektaktenWebGisLayout
{
    private static readonly IReadOnlyDictionary<string, string> Bereiche = LadeBereiche();
    private static readonly string[] Reihenfolge = ["Daten", "Daten I", "Daten II", "Bauwerksteile", "Haltungspunkte",
        "Stammkarte", "Bewertung", "Attribute", "Objekte referenzieren", "Darstellung", "Administrativ", "Einbauten",
        "Unterhalt", "Hydraulik", "Anschluss", "Video", "Sanierungsdetail", "Metadaten", "SewerStudio"];

    public static string Bereich(ObjektFeldDefinition feld)
    {
        if (Bereiche.TryGetValue(feld.Id, out var bereich)) return bereich;
        if (feld.Id.StartsWith("haltung.reliner_", StringComparison.Ordinal)) return "Bauwerksteile";
        if (feld.Id is "haltung.haltungsbemerkung" or "schacht.knotenbemerkung") return "Daten I";
        if (feld.Art == "sanierung") return "Sanierungsdetail";
        if (feld.WebgisKennung is not null && Reihenfolge.Contains(feld.Gruppe)) return feld.Gruppe;
        return "SewerStudio";
    }

    public static IReadOnlyList<ObjektWebGisAbschnitt> Erstelle(string art, IEnumerable<ObjektFeldViewModel> felder,
        IEnumerable<ObjektListenAnzeige> listen, AppSettings settings, bool suche)
    {
        var feldgruppen = felder.Where(f => Bereich(f.Feld) != "Kopf").ToLookup(f => Bereich(f.Feld));
        var listengruppen = listen.ToLookup(ListenBereich);
        return Reihenfolge.Where(b => feldgruppen.Contains(b) || listengruppen.Contains(b))
            .Select(b => new ObjektWebGisAbschnitt(b, feldgruppen[b].ToArray(), listengruppen[b].ToArray(),
                settings, "webgis." + art + "." + b, suche)).ToArray();
    }

    private static string ListenBereich(string titel) => titel switch
    {
        "Unterhaltsmassnahmen" or "Sanierungsmassnahmen" or "Dichtheitsprüfungen" or "Inspektionen" or "GEP Massnahmen" => "Unterhalt",
        "Einzugsgebiete SW" or "Einzugsgebiete RW" or "Einzugsgebiete MW" => "Hydraulik",
        _ => "Bauwerksteile"
    };

    private static string ListenBereich(ObjektListenAnzeige liste) => liste.WebgisAbschnitt is { } abschnitt
        && Reihenfolge.Contains(abschnitt) ? abschnitt : ListenBereich(liste.Titel);

    public static void AktualisiereListen(IEnumerable<ObjektWebGisAbschnitt> abschnitte, IEnumerable<ObjektListenAnzeige> listen)
    {
        var gruppen = listen.ToLookup(ListenBereich);
        foreach (var abschnitt in abschnitte) abschnitt.SetzeListen(gruppen[abschnitt.Titel].ToArray());
    }

    private static IReadOnlyDictionary<string, string> LadeBereiche()
    {
        using var stream = typeof(ObjektaktenWebGisLayout).Assembly.GetManifestResourceStream("ObjektaktenWebGisBereiche.json")
            ?? throw new InvalidOperationException("Die WebGIS-Feldbereiche fehlen.");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidOperationException("Die WebGIS-Feldbereiche sind leer.");
    }
}

public sealed class ObjektWebGisAbschnitt(string titel, IReadOnlyList<ObjektFeldViewModel> felder,
    IReadOnlyList<ObjektListenAnzeige> listen, AppSettings settings, string schluessel, bool suche) : ObservableObject
{
    private bool? _offen;
    private IReadOnlyList<ObjektListenAnzeige> _listen = listen;
    public string Titel => titel;
    public string Umfang => felder.Count > 0 ? $"{felder.Count} Felder" : $"{_listen.Count} Listen";
    public IReadOnlyList<ObjektFeldViewModel> Felder => felder;
    public IReadOnlyList<ObjektListenAnzeige> Listen => _listen;
    public void SetzeListen(IReadOnlyList<ObjektListenAnzeige> wert)
    {
        _listen = wert;
        OnPropertyChanged(nameof(Listen)); OnPropertyChanged(nameof(Umfang));
    }
    public bool Offen
    {
        get => _offen ?? (suche || settings.ObjektakteGruppen.GetValueOrDefault(schluessel));
        set
        {
            _offen = value;
            if (!suche) settings.ObjektakteGruppen[schluessel] = value;
            OnPropertyChanged();
        }
    }
}
