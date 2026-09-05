namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Die Objektkennungen, unter denen GEONIS ein Bauteil fuehrt (<c>SIA405_ID</c>,
/// 16 Zeichen, Praefix <c>ch23h1a4</c>).
///
/// Warum das ein eigenes Objekt ist und kein Feld: Eine Haltung ist in SIA405 kein
/// einzelnes Objekt, sondern ein Verbund aus Kanal, Haltung, zwei Haltungspunkten
/// und Rohrprofil — jedes mit eigener Kennung. Ein Schacht besteht aus Bauwerk und
/// Abwasserknoten. Erst mit ALLEN Kennungen kann der Neu-Export eine Datei
/// schreiben, deren Objekte GEONIS wiedererkennt, statt Duplikate anzulegen.
///
/// Rein zusaetzliche Angabe. Altprojekte laden mit <c>null</c>; das sichtbare Feld
/// <c>Objekt_ID</c> bleibt davon unberuehrt (dort steht bei aus QGIS gefuellten
/// Haltungen die Lisag-Nummer aus dem WFS-Dienst geo.ur.ch, die bei jeder
/// Veroeffentlichung neu vergeben wird und in GEONIS nicht existiert — gemessen 2026-09-04: 866789 wurde zu 867034).
///
/// Fachwerte werden ueber diesen Weg nie uebernommen, nur Kennungen.
/// </summary>
public sealed class GeonisKennungen
{
    /// <summary>Kennung der Haltung (Klasse <c>Haltung</c>).</summary>
    public string? Haltung { get; set; }

    /// <summary>Kennung des logischen Kanals (<c>Kanal</c>), auf den die Haltung verweist.</summary>
    public string? Kanal { get; set; }

    /// <summary>Kennung des Haltungspunkts am oberen Schacht der Haltung, wie sie im Projekt steht.</summary>
    public string? VonPunkt { get; set; }

    /// <summary>Die Bezeichnung, die GEONIS diesem Haltungspunkt gibt (z. B. <c>A75394</c>).</summary>
    public string? VonPunktBezeichnung { get; set; }

    /// <summary>Kennung des Haltungspunkts am unteren Schacht der Haltung, wie sie im Projekt steht.</summary>
    public string? NachPunkt { get; set; }

    /// <summary>Die Bezeichnung, die GEONIS diesem Haltungspunkt gibt (z. B. <c>E75394</c>).</summary>
    public string? NachPunktBezeichnung { get; set; }

    /// <summary>Kennung des Rohrprofils, auf das die Haltung in GEONIS verweist.</summary>
    public string? Rohrprofil { get; set; }

    /// <summary>
    /// Der Profiltyp dieses GEONIS-Rohrprofils in Normschreibweise (<c>Kreisprofil</c>,
    /// <c>unbekannt</c>, …). Nur bei gleichem Typ darf der Export die Profilkennung
    /// wiederverwenden: Ein Rohrprofil wird in GEONIS von vielen Haltungen geteilt.
    /// </summary>
    public string? RohrprofilTyp { get; set; }

    /// <summary>Kennung des Abwasserknotens (<c>Abwasserknoten</c>) eines Schachts.</summary>
    public string? Knoten { get; set; }

    /// <summary>Kennung des Bauwerks (<c>Normschacht</c>) eines Schachts.</summary>
    public string? Bauwerk { get; set; }

    /// <summary>
    /// True, wenn die Haltung im Projekt in der Gegenrichtung zum Kataster heisst
    /// ("B-A" gegen "A-B"). Die Punktkennungen sind dann bereits vertauscht, damit
    /// jeder Punkt am richtigen Schacht bleibt.
    /// </summary>
    public bool RichtungGedreht { get; set; }

    /// <summary>Woher die Kennungen stammen, z. B. der Stand der GEONIS-Kopie.</summary>
    public string? Quelle { get; set; }

    /// <summary>
    /// Wann das Objekt in GEONIS zuletzt geaendert wurde, zum Zeitpunkt der Quelle
    /// (<c>GN_LAST_EDITED_DATE</c>). Das ist der Ausgangsstand fuer den Konfliktschutz
    /// beim Import: Ist das Objekt in GEONIS seither geaendert worden, darf eine
    /// aeltere XTF es nicht ueberschreiben. SewerStudio traegt den Wert mit; die
    /// Pruefung selbst liegt beim Import in GEONIS.
    /// </summary>
    public DateTime? GeonisGeaendert { get; set; }

    /// <summary>Wann die Kennungen uebernommen wurden.</summary>
    public DateTime? UebernommenUtc { get; set; }

    /// <summary>True, wenn wenigstens die Hauptkennung des Bauteils bekannt ist.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HatHaltung => !string.IsNullOrWhiteSpace(Haltung);

    [System.Text.Json.Serialization.JsonIgnore]
    public bool HatKnoten => !string.IsNullOrWhiteSpace(Knoten);
}
