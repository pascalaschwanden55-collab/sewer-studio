using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using AuswertungPro.Next.Application.Dossiers.Lookup;

namespace AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

/// <summary>
/// Fragt das Telefonverzeichnis von search.ch ueber die offizielle
/// Schnittstelle ab.
///
/// Die Nutzungsbedingungen verbieten maschinelle MASSENabfragen ausdruecklich.
/// Erlaubt ist die einzelne, von einem Menschen ausgeloeste Abfrage mit eigenem
/// Schluessel. Deshalb:
///   - ohne Schluessel wird gar nichts abgefragt, statt die Webseite zu lesen,
///   - jeder Aufruf entspricht genau einer Suche,
///   - die Stapelanlage verwendet diesen Dienst nicht.
///
/// Ein uebernommener Wert traegt die Quellenangabe "Swisscom Directories AG".
/// </summary>
public sealed class SearchChDirectoryClient : IDirectoryLookup, IDisposable
{
    private const string Basis = "https://search.ch/tel/api/";

    /// <summary>Namensraum der dokumentierten Antwort.</summary>
    private static readonly XNamespace Tel = "http://tel.search.ch/api/spec/result/1.0/";
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    private readonly Func<string?> _leseSchluessel;
    private readonly HttpClient _client;
    private readonly bool _eigenerClient;

    public SearchChDirectoryClient(Func<string?> leseSchluessel, HttpClient? client = null)
    {
        _leseSchluessel = leseSchluessel ?? throw new ArgumentNullException(nameof(leseSchluessel));
        _eigenerClient = client is null;
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_leseSchluessel());

    public string Attribution => "Quelle: Swisscom Directories AG";

    public async Task<DirectoryLookupResult> FindAsync(
        string name, string town, CancellationToken ct = default)
    {
        var schluessel = _leseSchluessel();
        if (string.IsNullOrWhiteSpace(schluessel))
        {
            return new DirectoryLookupResult(
                Array.Empty<DirectoryEntry>(),
                "Für die Telefonsuche fehlt der Schlüssel von search.ch. "
                + "Er wird in den Einstellungen hinterlegt.");
        }

        if (string.IsNullOrWhiteSpace(name))
            return new DirectoryLookupResult(Array.Empty<DirectoryEntry>());

        var adresse = Basis
            + "?key=" + Uri.EscapeDataString(schluessel.Trim())
            + "&was=" + Uri.EscapeDataString(name.Trim())
            + "&wo=" + Uri.EscapeDataString((town ?? string.Empty).Trim())
            + "&maxnum=5";

        string inhalt;
        try
        {
            using var antwort = await _client
                .GetAsync(adresse, HttpCompletionOption.ResponseContentRead, ct)
                .ConfigureAwait(false);

            if (!antwort.IsSuccessStatusCode)
            {
                return new DirectoryLookupResult(
                    Array.Empty<DirectoryEntry>(),
                    "Die Telefonsuche antwortete mit " + (int)antwort.StatusCode + ".");
            }

            inhalt = await antwort.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new DirectoryLookupResult(
                Array.Empty<DirectoryEntry>(),
                "Die Telefonsuche war nicht erreichbar: " + ex.Message);
        }

        return new DirectoryLookupResult(Parse(inhalt));
    }

    /// <summary>
    /// Liest die Antwort. Was nicht sicher erkannt wird, ergibt KEINEN Eintrag —
    /// eine geratene Telefonnummer im Brief an den Eigentuemer waere schlimmer
    /// als eine leere Zelle.
    /// </summary>
    internal static IReadOnlyList<DirectoryEntry> Parse(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return Array.Empty<DirectoryEntry>();

        XDocument dokument;
        try
        {
            dokument = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return Array.Empty<DirectoryEntry>();
        }

        var ergebnis = new List<DirectoryEntry>();

        foreach (var eintrag in dokument.Descendants(Atom + "entry"))
        {
            string Feld(string name)
                => eintrag.Element(Tel + name)?.Value.Trim() ?? string.Empty;

            var anzeige = Feld("name");
            var vorname = Feld("firstname");
            var vollname = string.Join(" ", new[] { anzeige, vorname }
                .Where(t => t.Length > 0));

            if (vollname.Length == 0)
                vollname = eintrag.Element(Atom + "title")?.Value.Trim() ?? string.Empty;

            var telefon = Feld("phone");
            var mail = Feld("email");

            // Ein Eintrag ohne Name und ohne Nummer traegt nichts bei.
            if (vollname.Length == 0 || (telefon.Length == 0 && mail.Length == 0))
                continue;

            ergebnis.Add(new DirectoryEntry(
                vollname,
                string.Join(" ", new[] { Feld("street"), Feld("streetno") }
                    .Where(t => t.Length > 0)),
                Feld("zip"),
                Feld("city"),
                telefon,
                mail));
        }

        return ergebnis;
    }

    public void Dispose()
    {
        if (_eigenerClient)
            _client.Dispose();
    }
}
