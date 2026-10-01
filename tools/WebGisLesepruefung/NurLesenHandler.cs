using System.Net;
using System.Text;

namespace WebGisLesepruefung;

/// <summary>
/// Lässt ausschliesslich Leseaufrufe ans WebGIS durch. Jeder andere Aufruf — insbesondere
/// <c>saveData</c> — wird VOR dem Netz abgewiesen und gezählt. Das ist die technische Zusage,
/// dass diese Prüfung in der Datenbank nichts ändert:
/// <list type="bullet">
/// <item>GET nur auf getLayoutDataCombined, getEmptyData, getControlValues (lesende Endpunkte des Attributeditors).</item>
/// <item>POST nur an den synserver und nur mit den Suchaktionen GET_QUERY_FULL_TEXT und GET_RESULTS.</item>
/// </list>
/// Jede durchgelassene Antwort wird ohne Anmeldedaten in den Ausgabeordner geschrieben.
/// </summary>
internal sealed class NurLesenHandler : DelegatingHandler
{
    private static readonly string[] LeseEndpunkte = ["getLayoutDataCombined", "getEmptyData", "getControlValues"];
    private static readonly string[] SuchAktionen = ["serveraction=GET_QUERY_FULL_TEXT", "serveraction=GET_RESULTS"];
    private static readonly string[] Verboten = ["saveData", "delete", "insert", "update", "edit", "commit", "apply"];

    private readonly string _ausgabe;
    private int _nummer;

    public NurLesenHandler(HttpMessageHandler inner, string ausgabe) : base(inner) => _ausgabe = ausgabe;

    public int Abgewiesen { get; private set; }
    public List<string> Protokoll { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var url = request.RequestUri?.ToString() ?? string.Empty;
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct);
        var grund = PruefeLesend(request.Method, url, body);
        if (grund is not null)
        {
            Abgewiesen++;
            Protokoll.Add($"ABGEWIESEN {request.Method} {Endpunkt(url)}: {grund}");
            throw new InvalidOperationException("Nur-Lesen-Schutz: " + grund + " — Aufruf nicht gesendet.");
        }

        var antwort = await base.SendAsync(request, ct);
        await antwort.Content.LoadIntoBufferAsync(ct);
        var text = await antwort.Content.ReadAsStringAsync(ct);
        var nr = Interlocked.Increment(ref _nummer);
        var name = $"{nr:D3}_{Endpunkt(url)}_{Tabelle(url)}.json";
        await File.WriteAllTextAsync(Path.Combine(_ausgabe, name), text, new UTF8Encoding(false), ct);
        Protokoll.Add($"{nr:D3} {request.Method} {Endpunkt(url)} table={Tabelle(url)} -> HTTP {(int)antwort.StatusCode}, {text.Length} Zeichen");
        return antwort;
    }

    /// <summary>Null, wenn der Aufruf lesend ist; sonst der Grund für die Abweisung.</summary>
    internal static string? PruefeLesend(HttpMethod methode, string url, string body)
    {
        // Nur der letzte Adressteil zaehlt (der Editorpfad selbst heisst «attributeeditor»).
        var endpunkt = Endpunkt(url);
        foreach (var wort in Verboten)
            if (endpunkt.Contains(wort, StringComparison.OrdinalIgnoreCase))
                return $"Endpunkt «{endpunkt}» ist kein Leseaufruf";

        if (methode == HttpMethod.Get)
            return LeseEndpunkte.Any(e => string.Equals(endpunkt, e, StringComparison.OrdinalIgnoreCase))
                ? null
                : $"GET auf unbekannten Endpunkt «{endpunkt}»";

        if (methode == HttpMethod.Post && string.Equals(endpunkt, "synserver", StringComparison.OrdinalIgnoreCase))
        {
            var klar = WebUtility.UrlDecode(body);
            return SuchAktionen.Any(a => klar.Contains(a, StringComparison.Ordinal))
                ? null
                : "POST an den synserver ohne Suchaktion";
        }

        return $"{methode} auf «{Endpunkt(url)}» ist nicht als Leseaufruf freigegeben";
    }

    /// <summary>Letzter Adressteil ohne Abfrage und ohne «;jsessionid=…».</summary>
    internal static string Endpunkt(string url)
    {
        var pfad = url.Split('?')[0].TrimEnd('/');
        var i = pfad.LastIndexOf('/');
        var teil = i >= 0 ? pfad[(i + 1)..] : pfad;
        var semikolon = teil.IndexOf(';');
        return semikolon >= 0 ? teil[..semikolon] : teil;
    }

    private static string Tabelle(string url)
    {
        var abfrage = url.Contains('?') ? url[(url.IndexOf('?') + 1)..] : string.Empty;
        foreach (var teil in abfrage.Split('&'))
            if (teil.StartsWith("table=", StringComparison.OrdinalIgnoreCase))
                return teil[6..];
        return "-";
    }
}
