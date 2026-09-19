using System.Globalization;
using AuswertungPro.Next.Application.Lookup;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Die Bauteile eines Schachts, an denen ein Schaden im Schnitt verortet wird, von oben nach
/// unten. <see cref="Unbekannt"/> landet in der Schachtwand — ohne belegten Ort wird nichts
/// geraten (dieselbe Regel wie bisher fuer die Zone).
/// </summary>
public enum SchachtBauteil
{
    Deckel,
    Rahmen,
    Schachthals,
    Konus,
    Schachtrohr,
    Steigeisen,
    Anschluss,
    Unbekannt,
    Bankett,
    Durchlaufrinne,
    Sohle,
    Tauchbogen
}

/// <summary>
/// Was die Seite der Schachtgrafik zusaetzlich zum Datensatz reichen kann: die Lage aus der
/// QGIS-Kopie (Schachtpunkt, Azimut je Haltung) und die Koten aus den Objektakten. Beides ist
/// optional; ohne Zusatz bleibt die Grafik schematisch und sagt das.
/// </summary>
public sealed record SchachtgrafikZusatz(SchachtLage? Lage, SchachtKoten? Koten, string? LageHinweis = null)
{
    public static SchachtgrafikZusatz Leer { get; } = new(null, null);
}

/// <summary>Ein Anschluss, wie ihn Schnitt und Grundriss zeichnen.</summary>
/// <param name="Nr">Nummer aus der Anschlusstabelle; sonst fortlaufend vergeben.</param>
/// <param name="TiefeM">Rohrsohle ab Deckel-OK in Metern; <c>null</c> = nicht erfasst.</param>
/// <param name="TiefeQuelle">«Protokoll» oder «Kataster»; <c>null</c>, wenn keine Tiefe vorliegt.</param>
/// <param name="AzimutGrad">Richtung vom Schacht weg, 0 = Nord; <c>null</c> = nicht erfasst.</param>
/// <param name="KoteM">Rohrsohle in m ue. M. aus dem Kataster.</param>
/// <param name="ImProjekt">True, wenn eine Haltung des Projekts dazugehoert.</param>
public sealed record SchachtgrafikAnschluss(
    int Nr,
    bool IstAuslauf,
    int? DnMm,
    decimal? TiefeM,
    string? TiefeQuelle,
    string? Material,
    string? Haltungsname,
    double? AzimutGrad,
    decimal? KoteM,
    bool ImProjekt)
{
    /// <summary>«A1» fuer Auslauf 1, «E3» fuer Einlauf 3 — die Nummer der Anschlusstabelle.</summary>
    public string Kennung => (IstAuslauf ? "A" : "E") + Nr.ToString(CultureInfo.InvariantCulture);

    /// <summary>Die volle Beschriftung fuer Legende und Hinweisflaeche.</summary>
    public string Beschreibung
    {
        get
        {
            var teile = new List<string> { Kennung + (IstAuslauf ? " Auslauf" : " Einlauf") };
            if (!string.IsNullOrWhiteSpace(Haltungsname))
                teile.Add(Haltungsname.Trim());
            if (DnMm is { } dn)
                teile.Add("DN " + dn.ToString(CultureInfo.InvariantCulture));
            teile.Add(TiefeM is { } t
                ? t.ToString("0.00", CultureInfo.InvariantCulture) + " m" + (TiefeQuelle is null ? "" : " (" + TiefeQuelle + ")")
                : "Tiefe nicht erfasst");
            if (!string.IsNullOrWhiteSpace(Material))
                teile.Add(Material.Trim());
            if (!ImProjekt)
                teile.Add("nicht im Projekt");
            return string.Join(" · ", teile);
        }
    }
}

/// <summary>Ein Schaden mit seiner Nummer in der Legende und seinem Ort.</summary>
public sealed record SchachtgrafikSchaden(
    int Nr,
    SchachtBauteil Bauteil,
    int? AnschlussNr,
    string Kategorie,
    string Farbe,
    string Tooltip);

/// <summary>
/// Alles, was die Schachtgrafik zeichnet — WPF-frei und ohne erfundene Werte: Was fehlt, ist
/// <c>null</c> und steht in <see cref="Hinweise"/> als Vermerk.
/// </summary>
public sealed record SchachtgrafikModell(
    string? Schachtnummer,
    decimal? TiefeM,
    string? TiefeQuelle,
    int? Dimension1Mm,
    int? Dimension2Mm,
    string? Schachtform,
    int? DeckelDurchmesserMm,
    string? DeckelMaterial,
    string? Nutzungsart,
    bool? SteigeisenVorhanden,
    SchachtKoten? Koten,
    IReadOnlyList<SchachtgrafikAnschluss> Anschluesse,
    IReadOnlyList<SchachtgrafikSchaden> Schaeden,
    IReadOnlyList<string> Hinweise)
{
    /// <summary>Der Auslauf, an dem sich Schnitt und Grundriss ausrichten: der tiefste, sonst der erste.</summary>
    public SchachtgrafikAnschluss? Hauptauslauf => Anschluesse
        .Where(a => a.IstAuslauf)
        .OrderByDescending(a => a.TiefeM ?? decimal.MinValue)
        .ThenBy(a => a.Nr)
        .FirstOrDefault();

    public bool HatRichtungen => Anschluesse.Any(a => a.AzimutGrad is not null);

    public bool HatMasse => Dimension1Mm is > 0 && Dimension2Mm is > 0;
}

/// <summary>Eine Zeile der Legende unter der Grafik: Marke (Kennung oder Nummer), ihre Farbe und der Text.</summary>
public sealed record SchachtgrafikLegende(string Marke, string Farbe, string Text);
