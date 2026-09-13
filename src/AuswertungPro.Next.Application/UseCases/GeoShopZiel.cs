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
                && _projekt.SchaechteData.Count(r => string.Equals(r.GetFieldValue(SchachtFeldnamen.Feld(r, "Schachtnummer")).Trim(), name, StringComparison.OrdinalIgnoreCase)) == 1;
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

    public static GeoShopZiel Fuer(SchachtRecord r) => new(r, BauteilArt.Schacht,
        f => r.GetFieldValue(SchachtFeldnamen.Feld(r, f)), f => r.IsUserEdited(SchachtFeldnamen.Feld(r, f)),
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
        if (_projekt is not null)
            Objektakten.GeoShopObjektaktenImport.Uebernehme(_projekt, _id, Art == BauteilArt.Haltung ? "haltung" : "schacht", position.Quelle, position.Gedreht);
    }
}
