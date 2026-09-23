using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>Ergebnis eines Schreibvorgangs ins WebGIS.</summary>
public sealed class WebGisSchreibErgebnis
{
    public bool Erfolg { get; init; }
    public string? Fehler { get; init; }
    /// <summary>Beim Anlegen: die vom WebGIS vergebene Objekt-ID.</summary>
    public string? NeueId { get; init; }

    public static WebGisSchreibErgebnis Ok(string? neueId = null) => new() { Erfolg = true, NeueId = neueId };
    public static WebGisSchreibErgebnis Fehlgeschlagen(string grund) => new() { Erfolg = false, Fehler = grund };
}

/// <summary>
/// Zugriff auf den GEONIS-Attributeditor im WebGIS (WebOffice) ueber dessen interne
/// Schnittstelle: Anmeldung, GlobalID-Aufloesung ueber die Suche, Lesen der
/// Feldwerte und Schreiben genau der geaenderten Felder.
///
/// Die Implementierung liegt in Infrastructure; hier steht nur der Vertrag, damit
/// Planbau und Ablauf ohne Netz getestet werden koennen.
/// </summary>
public interface IGeonisWebGisClient
{
    /// <summary>
    /// Loest die Bezeichnung ueber die WebGIS-Suche zu genau einem Objekt auf und
    /// liest dessen aktuelle Feldwerte (je refId). Null, wenn kein eindeutiger
    /// Treffer existiert.
    /// </summary>
    Task<WebGisLesestand?> LeseAsync(
        WebGisObjektart art, string bezeichnung, CancellationToken ct = default);

    /// <summary>
    /// Liest das Objekt direkt ueber seine GlobalID, OHNE Namenssuche (Pascal 23.09.2026).
    /// <see cref="WebGisLesestand.Bezeichnung"/> ist dann der Name aus der Maske (leer, wenn die
    /// Maske ihn nicht liefert) — der Aufrufer prueft ihn gegen den Projektnamen. Null, wenn das
    /// Objekt nicht lesbar ist.
    /// </summary>
    Task<WebGisLesestand?> LeseUeberGlobalIdAsync(
        WebGisObjektart art, string globalId, CancellationToken ct = default);

    /// <summary>
    /// Schreibt die genannten Felder (refId -&gt; Wert; Combo als Ganzzahl-Schluessel
    /// als Text) am Objekt mit der GlobalID. GEONIS prueft die Norm-Regeln.
    /// </summary>
    Task<WebGisSchreibErgebnis> SchreibeAsync(
        WebGisObjektart art, string globalId,
        IReadOnlyDictionary<string, string> felder, CancellationToken ct = default);

    /// <summary>
    /// Liest die Combo-Kataloge der Maske "Sanierungsmassnahme" (leeres Objekt am
    /// Elternobjekt). Null, wenn nicht lesbar.
    /// </summary>
    Task<WebGisSanierungKatalog?> LeseSanierungKatalogAsync(
        WebGisObjektart art, string elternGlobalId, CancellationToken ct = default);

    /// <summary>
    /// Legt eine neue Sanierungsmassnahme (AWZ_UNTERHALT, art=4) am Elternobjekt an.
    /// <paramref name="felder"/>: refId -&gt; Wert (Combo-Schluessel als Text, Datum ISO).
    /// Liefert bei Erfolg die neue Objekt-ID in <see cref="WebGisSchreibErgebnis.NeueId"/>.
    /// </summary>
    Task<WebGisSchreibErgebnis> ErstelleSanierungAsync(
        WebGisObjektart art, string elternGlobalId,
        IReadOnlyDictionary<string, string> felder, CancellationToken ct = default);

    /// <summary>
    /// Liest die von einem Elternwert abhaengige Auswahlliste eines Combo-Felds der
    /// Objektmaske (getControlValues mit filter), z.B. die Material-Details der Gruppe
    /// «Beton». Null, wenn nicht lesbar.
    /// </summary>
    Task<IReadOnlyList<(string Key, string Text)>?> LeseKatalogListeAsync(
        WebGisObjektart art, string refId, string filter, string? subtyp = null, CancellationToken ct = default);

    /// <summary>
    /// Liest eine Sanierungsmassnahme (AWZ_UNTERHALT) ueber ihre GlobalId — fuer das Holen der
    /// Massnahmen nach SewerStudio (23.09.2026). Felder und Auswahllisten wie <see cref="LeseAsync"/>.
    /// Null, wenn nicht lesbar.
    /// </summary>
    Task<WebGisLesestand?> LeseMassnahmeAsync(string globalId, CancellationToken ct = default)
        => Task.FromResult<WebGisLesestand?>(null);
}
