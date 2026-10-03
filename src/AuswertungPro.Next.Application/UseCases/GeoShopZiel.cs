using System;
using System.Text.Json;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases;

/// <summary>Ein bestehender Projektdatensatz. Der Abgleich legt niemals neue Datensaetze an.</summary>
public sealed class GeoShopZiel
{
    public object Datensatz { get; }
    public BauteilArt Art { get; }
    public string Name => Wert(Art == BauteilArt.Haltung ? FieldKeys.HoldingName : "Schachtnummer").Trim();
    public Func<string, string> Wert { get; }
    public Func<string, bool> Handgesetzt { get; }
    public Func<string> Stand { get; }
    public Func<GeonisKennungen?> Kennungen { get; }
    private readonly Action<GeonisKennungen> _setzeKennungen;
    private readonly Action<string, string> _setze;
    private readonly Func<string, string, bool> _fuelle;
    private readonly Action<string, string> _ersetze;
    private Project? _projekt;
    private Guid _id;
    public Project? Projekt => _projekt;
    public Guid Id => _id;
    public string Herkunft(string feld)
    {
        // Am Schacht von derselben Schreibweise, die Wert liefert (Review PR #82).
        var meta = Datensatz is SchachtRecord s
            ? s.FieldMeta.GetValueOrDefault(SchachtFeldnamen.AktuelleSchreibweise(s, feld) ?? SchachtFeldnamen.Feld(s, feld))
            : ((HaltungRecord)Datensatz).FieldMeta.GetValueOrDefault(feld);
        return meta?.UserEdited == true ? "Handeingabe" : meta?.Source switch
        {
            FieldSource.Legacy => "Alter Import", FieldSource.Kataster => "Kataster",
            FieldSource.Xtf or FieldSource.Xtf405 => "XTF", FieldSource.Pdf => "PDF",
            FieldSource.Spro => "SchachtPro", FieldSource.Protocol => "Protokoll",
            _ => string.IsNullOrWhiteSpace(Wert(feld)) ? "Leer" : "Herkunft nicht eindeutig"
        };
    }
    internal void SchreibeVergleich(string feld, string wert)
    {
        if (Handgesetzt(feld)) throw new InvalidOperationException("Eine Handeingabe darf nicht automatisch ersetzt werden.");
        _setze(feld, wert);
        // Der Datensatz speichert seit 23.09.2026 den WebGIS-Begriff («unbekannt» -> «Unbekannt»); das
        // ist derselbe Wert und kein Fehlschlag.
        var geschrieben = Wert(feld);
        if (geschrieben != wert && geschrieben != WebGisBegriffe.Normalisieren(Datensatz is SchachtRecord, feld, wert))
            throw new InvalidOperationException($"{Name}: {feld} konnte nicht übernommen werden.");
    }
    /// <summary>Auch der Einzelweg prueft die Namen im ganzen gebundenen Projekt.
    /// Beim Anwenden erneut lesen: Eine inzwischen umbenannte Nachbarzeile kann den Namen doppeln.</summary>
    internal bool ProjektnameEindeutig
    {
        get
        {
            if (_projekt is null) return true; // Alte Aufrufer liefern ihren Bestand an den gemeinsamen Planer.
            var name = Name;
            if (Art == BauteilArt.Haltung)
                return Datensatz is HaltungRecord h && _projekt.Data.Contains(h)
                    && _projekt.Data.Count(r => string.Equals(r.GetFieldValue(FieldKeys.HoldingName).Trim(), name, StringComparison.OrdinalIgnoreCase)) == 1;
            return Datensatz is SchachtRecord s && _projekt.SchaechteData.Contains(s)
                // Dieselbe Regel wie Name (SchachtFeldnamen.Wert), sonst sieht die Pruefung beim eigenen
                // Datensatz einen anderen Namen (Review PR #82).
                && _projekt.SchaechteData.Count(r => string.Equals(SchachtFeldnamen.Wert(r, "Schachtnummer").Trim(), name, StringComparison.OrdinalIgnoreCase)) == 1;
        }
    }
    public bool HatNeueAktenwerte(GeoShopBauteil quelle) => _projekt is not null
        && Objektakten.GeoShopObjektaktenImport.HatNeueQuellen(_projekt, _id, quelle);
    public string Aktenstand => _projekt is null ? "" : Objektakten.GeoShopObjektaktenImport.Stand(_projekt, _id);

    public static GeoShopZiel Fuer(HaltungRecord r, Project projekt)
    { var z = Fuer(r); z._projekt = projekt; z._id = r.Id; return z; }
    public static GeoShopZiel Fuer(SchachtRecord r, Project projekt)
    { var z = Fuer(r); z._projekt = projekt; z._id = r.Id; return z; }

    private GeoShopZiel(object datensatz, BauteilArt art, Func<string, string> wert,
        Func<string, bool> handgesetzt, Func<string> stand, Func<GeonisKennungen?> kennungen,
        Action<GeonisKennungen> setzeKennungen, Action<string, string> setze, Func<string, string, bool> fuelle,
        Action<string, string> ersetze)
    {
        Datensatz = datensatz; Art = art; Wert = wert; Handgesetzt = handgesetzt; Stand = stand;
        Kennungen = kennungen; _setzeKennungen = setzeKennungen; _setze = setze; _fuelle = fuelle; _ersetze = ersetze;
    }

    public static GeoShopZiel Fuer(HaltungRecord r) => new(r, BauteilArt.Haltung,
        r.GetFieldValue, f => r.FieldMeta.TryGetValue(f, out var m) && m.UserEdited,
        () => JsonSerializer.Serialize(new { r.Fields, r.FieldMeta, r.Geonis }), () => r.Geonis,
        r.SetzeGeonisKennungen, (f, w) => r.SetFieldValue(f, w, FieldSource.Kataster, false),
        (f, w) => r.FuelleLeeresFeld(f, w, FieldSource.Kataster),
        (f, w) =>
        {
            // Ersetzen heisst auch ueber eine Handmarkierung hinweg: der Katasterwert gewinnt und
            // gilt danach nicht mehr als Handaenderung (ginge sonst in die XTF-Aenderungslieferung).
            if (r.FieldMeta.TryGetValue(f, out var meta)) meta.UserEdited = false;
            r.SetFieldValue(f, w, FieldSource.Kataster, false);
        });

    // Planen ueber alle Schreibweisen (Planer-Paket nach PR #81): Ist-Wert wie der Export
    // (SchachtFeldnamen.Wert), geschuetzt ist das Feld, wenn irgendeine Schreibweise ein Handwert
    // ist (auch bewusst leer) - genau dann lehnt der Schreibweg ab. Das Schreibziel bleibt Feld.
    public static GeoShopZiel Fuer(SchachtRecord r) => new(r, BauteilArt.Schacht,
        f => SchachtFeldnamen.Wert(r, f), f => SchachtFeldnamen.HatHandwert(r, f),
        () => JsonSerializer.Serialize(new { r.Fields, r.FieldMeta, r.Geonis }), () => r.Geonis,
        r.SetzeGeonisKennungen, (f, w) => r.SetFieldValue(SchachtFeldnamen.Feld(r, f), w, FieldSource.Kataster, false),
        (f, w) => r.FuelleLeeresFeld(SchachtFeldnamen.Feld(r, f), w, FieldSource.Kataster),
        (f, w) =>
        {
            var name = SchachtFeldnamen.Feld(r, f);
            if (r.FieldMeta.TryGetValue(name, out var meta)) meta.UserEdited = false;
            r.SetFieldValue(name, w, FieldSource.Kataster, false);
        });

    internal void Uebernehme(GeoShopPosition position, string quelle)
    {
        if (position.KennungenAendern)
        {
            var k = GeoShopAbgleichPlanBuilder.Kennungen(position.Quelle.Kennungen, position.Gedreht);
            k.Quelle = $"GeoShop-XTF: {quelle}";
            k.UebernommenUtc = DateTime.UtcNow;
            _setzeKennungen(k);
        }
        foreach (var feld in position.Felder)
        {
            if (feld.IstKennung && !string.IsNullOrWhiteSpace(Wert(feld.Feld))) _setze(feld.Feld, feld.Nachher);
            else if (feld.Ersetzen) _ersetze(feld.Feld, feld.Nachher);
            else _fuelle(feld.Feld, feld.Nachher);
        }
        if (position.Vergleich is not null) position.Vergleich.Uebernehme(this);
        else if (_projekt is not null)
            Objektakten.GeoShopObjektaktenImport.Uebernehme(_projekt, _id, Art == BauteilArt.Haltung ? "haltung" : "schacht", position.Quelle, position.Gedreht);
    }
}
