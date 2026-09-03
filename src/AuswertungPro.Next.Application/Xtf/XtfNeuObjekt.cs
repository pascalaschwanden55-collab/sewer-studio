using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AuswertungPro.Next.Application.Xtf;

/// <summary>Ein Punkt in Landeskoordinaten LV95 (EPSG:2056).</summary>
public sealed record XtfPunkt(double Ost, double Nord)
{
    public string OstText => Ost.ToString("0.000", CultureInfo.InvariantCulture);
    public string NordText => Nord.ToString("0.000", CultureInfo.InvariantCulture);
}

/// <summary>
/// Die Geometrie eines Objekts: ein Punkt (<c>Lage</c>) oder ein Linienzug (<c>Verlauf</c>).
/// Fehlt sie, wird nichts geschrieben — SIA405 verlangt sie bei keiner der hier
/// erzeugten Klassen.
/// </summary>
public sealed record XtfNeuGeometrie(string Feldname, IReadOnlyList<XtfPunkt> Punkte)
{
    public bool IstLinie => Punkte.Count > 1;
}

/// <summary>Ein Verweis auf ein anderes Objekt derselben Datei.</summary>
public sealed record XtfNeuVerweis(string Name, string ZielTid);

/// <summary>
/// Ein fertig geplantes XTF-Objekt. Reines Ergebnis einer Rechnung: Der Bauer entscheidet
/// alles, der Schreiber setzt es nur noch in XML um.
/// </summary>
public sealed record XtfNeuObjekt(
    string Klasse,
    string Tid,
    IReadOnlyList<KeyValuePair<string, string>> Felder,
    IReadOnlyList<XtfNeuVerweis> Verweise,
    XtfNeuGeometrie? Geometrie = null,
    bool ImTopicAdministration = false);

/// <summary>
/// Vergibt die Objektkennungen (<c>TID</c>/<c>OBJ_ID</c>) fuer eine neu erzeugte XTF.
///
/// Zwei Anforderungen, die beide erfuellt sein muessen:
///
/// 1. <c>STANDARDOID</c> ist in INTERLIS <c>OID TEXT*16</c> — genau sechzehn Zeichen,
///    nur Ziffern und Buchstaben, beginnend mit einem Buchstaben. Eine kuerzere weist
///    der ilivalidator mit "is not a valid OID" ab.
///
/// 2. Die Kennung muss ueber Laeufe hinweg DIESELBE bleiben. Waere sie zufaellig oder
///    ein blosser Zaehler, legte das Zielsystem bei jedem Export neue Objekte an, statt
///    die vorhandenen zu aktualisieren — aus einer Korrektur wuerde eine Verdopplung.
///    Sie wird deshalb aus Projekt, Klasse und fachlichem Schluessel abgeleitet und
///    aendert sich nur, wenn einer dieser drei Werte sich aendert.
///
/// Der Praefix <c>chSST</c> haelt die eigenen Kennungen von denen des Katasters
/// getrennt; die beginnen mit <c>ch1000</c>.
/// </summary>
public sealed class XtfNeuKennungen
{
    private const string Praefix = "chSST";
    private const int Laenge = 16;
    private const int Stellen = Laenge - 5;

    private readonly string _projekt;
    private readonly Dictionary<string, string> _vergeben = new(StringComparer.Ordinal);
    private readonly HashSet<string> _benutzt = new(StringComparer.Ordinal);

    public XtfNeuKennungen(string? projektKennung)
        => _projekt = (projektKennung ?? "").Trim();

    /// <summary>
    /// Die Kennung fuer ein Objekt. Derselbe Aufruf liefert immer dieselbe Kennung —
    /// innerhalb eines Laufs und ueber Laeufe hinweg.
    /// </summary>
    public string Fuer(string klasse, string schluessel)
    {
        var merkmal = $"{_projekt}|{klasse}|{schluessel}";
        if (_vergeben.TryGetValue(merkmal, out var bekannt))
            return bekannt;

        var kennung = Berechne(merkmal);
        _vergeben[merkmal] = kennung;
        return kennung;
    }

    /// <summary>
    /// Elf Hexstellen aus dem SHA-256 des Merkmals. Bei einem Zusammenstoss — rechnerisch
    /// erst bei Millionen Objekten zu erwarten — wird weitergezaehlt, damit zwei Objekte
    /// nie dieselbe Kennung tragen.
    /// </summary>
    private string Berechne(string merkmal)
    {
        for (var versuch = 0; versuch < 1000; versuch++)
        {
            var quelle = versuch == 0 ? merkmal : $"{merkmal}#{versuch}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(quelle));
            var kennung = Praefix + Convert.ToHexString(hash)[..Stellen];

            if (_benutzt.Add(kennung))
                return kennung;
        }

        throw new InvalidOperationException(
            $"Fuer \"{merkmal}\" konnte keine freie Objektkennung vergeben werden.");
    }
}
