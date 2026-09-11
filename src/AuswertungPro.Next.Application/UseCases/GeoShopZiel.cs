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
    private Project? _projekt;
    private Guid _id;
    public bool HatNeueAktenwerte(GeoShopBauteil quelle) => _projekt is not null
        && Objektakten.GeoShopObjektaktenImport.HatNeueQuellen(_projekt, _id, quelle);
    public string Aktenstand => _projekt is null ? "" : Objektakten.GeoShopObjektaktenImport.Stand(_projekt, _id);

    public static GeoShopZiel Fuer(HaltungRecord r, Project projekt)
    { var z = Fuer(r); z._projekt = projekt; z._id = r.Id; return z; }
    public static GeoShopZiel Fuer(SchachtRecord r, Project projekt)
    { var z = Fuer(r); z._projekt = projekt; z._id = r.Id; return z; }

    private GeoShopZiel(object datensatz, BauteilArt art, Func<string, string> wert,
        Func<string, bool> handgesetzt, Func<string> stand, Func<GeonisKennungen?> kennungen,
        Action<GeonisKennungen> setzeKennungen, Action<string, string> setze, Func<string, string, bool> fuelle)
    {
        Datensatz = datensatz; Art = art; Wert = wert; Handgesetzt = handgesetzt; Stand = stand;
        Kennungen = kennungen; _setzeKennungen = setzeKennungen; _setze = setze; _fuelle = fuelle;
    }

    public static GeoShopZiel Fuer(HaltungRecord r) => new(r, BauteilArt.Haltung,
        r.GetFieldValue, f => r.FieldMeta.TryGetValue(f, out var m) && m.UserEdited,
        () => JsonSerializer.Serialize(new { r.Fields, r.FieldMeta, r.Geonis }), () => r.Geonis,
        r.SetzeGeonisKennungen, (f, w) => r.SetFieldValue(f, w, FieldSource.Kataster, false),
        (f, w) => r.FuelleLeeresFeld(f, w, FieldSource.Kataster));

    public static GeoShopZiel Fuer(SchachtRecord r) => new(r, BauteilArt.Schacht,
        f => r.GetFieldValue(SchachtFeldnamen.Feld(r, f)), f => r.IsUserEdited(SchachtFeldnamen.Feld(r, f)),
        () => JsonSerializer.Serialize(new { r.Fields, r.FieldMeta, r.Geonis }), () => r.Geonis,
        r.SetzeGeonisKennungen, (f, w) => r.SetFieldValue(SchachtFeldnamen.Feld(r, f), w, FieldSource.Kataster, false),
        (f, w) => r.FuelleLeeresFeld(SchachtFeldnamen.Feld(r, f), w, FieldSource.Kataster));

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
            else _fuelle(feld.Feld, feld.Nachher);
        }
        if (_projekt is not null)
            Objektakten.GeoShopObjektaktenImport.Uebernehme(_projekt, _id, Art == BauteilArt.Haltung ? "haltung" : "schacht", position.Quelle, position.Gedreht);
    }
}
