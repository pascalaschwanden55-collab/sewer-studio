using System.Net;
using System.Text;
using System.Text.Json;
using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.Infrastructure.WebGis;

namespace WebGisLesepruefung;

/// <summary>
/// NUR-LESEN-Prüfung am echten WebGIS (Entscheid Pascal 28.09.2026: «Wichtig ist, dass nichts geändert wird
/// in der Datenbank. Nur die Logik verstehen und prüfen»). Anmeldung wie im Programm: sichtbarer Browser, der
/// Benutzer meldet sich selbst an. Jeder Aufruf geht durch <see cref="NurLesenHandler"/>, der alles ausser
/// Leseaufrufen vor dem Netz abweist. Es gibt in diesem Werkzeug keinen Aufruf von SchreibeAsync oder
/// ErstelleSanierungAsync.
///
/// Aufruf: WebGisLesepruefung --ausgabe &lt;Ordner&gt; [--haltung 80480-80478] [--schacht 80478]
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var ausgabe = Wert(args, "--ausgabe") ?? Path.Combine(Path.GetTempPath(), "webgis-lesepruefung-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        var haltung = Wert(args, "--haltung") ?? "80480-80478";
        var schacht = Wert(args, "--schacht") ?? "80478";
        if (!SchutzHaelt())
        {
            Console.WriteLine("Selbstprüfung des Nur-Lesen-Schutzes fehlgeschlagen – es wird nichts angefragt.");
            return 3;
        }
        Console.WriteLine("Selbstprüfung: Schreibaufrufe werden abgewiesen, Leseaufrufe durchgelassen.");
        if (args.Contains("--nur-selbstpruefung")) return 0;

        Directory.CreateDirectory(ausgabe);
        Console.WriteLine($"Ausgabe: {ausgabe}");

        var einstellungen = LiesEinstellungen();
        var kontext = WebGisSynKontext.AusEinstellungen(einstellungen.Login, einstellungen.Rollen, einstellungen.Gruppen);

        await using var anmeldung = new PlaywrightWebGisAnmeldung();
        var fortschritt = new Progress<string>(t => Console.WriteLine("  " + t));
        Console.WriteLine("Öffne den Browser – bitte dort am WebGIS anmelden …");
        var zugang = await anmeldung.AnmeldenAsync(einstellungen.Basis, einstellungen.Projekt, einstellungen.Datenquelle,
            kontext, fortschritt, TimeSpan.FromMinutes(15));
        if (zugang is null)
        {
            Console.WriteLine("Anmeldung abgebrochen – nichts gelesen.");
            return 2;
        }

        var cookies = new CookieContainer();
        foreach (var c in zugang.Cookies)
        {
            try { cookies.Add(new Cookie(c.Name, c.Wert, string.IsNullOrEmpty(c.Pfad) ? "/" : c.Pfad, c.Domain.TrimStart('.'))); }
            catch (CookieException) { /* ungueltiges Cookie auslassen, wie im Programm */ }
        }

        var schutz = new NurLesenHandler(
            new HttpClientHandler { CookieContainer = cookies, UseCookies = true, AllowAutoRedirect = true }, ausgabe);
        using var http = new HttpClient(schutz) { Timeout = TimeSpan.FromSeconds(60) };
        var client = new GeonisWebGisClient(http, () => zugang);

        var bericht = new StringBuilder();
        void Zeile(string t) { Console.WriteLine(t); bericht.AppendLine(t); }

        foreach (var (art, name) in new[] { (WebGisObjektart.Haltung, haltung), (WebGisObjektart.Schacht, schacht) })
        {
            Zeile("");
            Zeile($"=== {art} {name} ===");
            WebGisLesestand? stand;
            try { stand = await client.LeseAsync(art, name); }
            catch (Exception ex) { Zeile("  Lesefehler: " + ex.Message); continue; }
            if (stand is null) { Zeile("  nicht eindeutig gefunden"); continue; }

            Zeile($"  GlobalID: {stand.GlobalId}");
            Zeile($"  Felder: {stand.Felder.Count}, Auswahllisten: {stand.Kataloge.Count}, Subtyp: {stand.Subtyp ?? "-"}");
            Zeile($"  Sanierungsmassnahmen in der Liste: {stand.Sanierungen.Count}");
            foreach (var z in stand.Sanierungen)
                Zeile($"    Beginn={z.Beginn ?? "-"} | Art={z.Art ?? "-"} | Status={z.Status ?? "-"} | Verfahren={z.Verfahren ?? "-"} | GlobalId={z.GlobalId ?? "-"}");

            foreach (var z in stand.Sanierungen.Where(z => !string.IsNullOrWhiteSpace(z.GlobalId)))
            {
                try
                {
                    var massnahme = await client.LeseMassnahmeAsync(z.GlobalId!);
                    Zeile($"    Massnahme {z.GlobalId}: {(massnahme is null ? "nicht lesbar" : massnahme.Felder.Count + " Felder")}");
                }
                catch (Exception ex) { Zeile($"    Massnahme {z.GlobalId}: Lesefehler {ex.Message}"); }
            }

            try
            {
                var katalog = await client.LeseSanierungKatalogAsync(art, stand.GlobalId);
                Zeile($"  Sanierungskatalog: {(katalog is null ? "nicht vollständig lesbar" : "gelesen")}");
            }
            catch (Exception ex) { Zeile("  Sanierungskatalog: Lesefehler " + ex.Message); }
        }

        Zeile("");
        Zeile("=== Kennungen in den Massnahmen-Antworten (Frage: ist newId eine OBJECTID oder GlobalID?) ===");
        foreach (var datei in Directory.GetFiles(ausgabe, "*getLayoutDataCombined_AWZ_UNTERHALT.json").OrderBy(d => d))
            Zeile("  " + Path.GetFileName(datei) + ": " + KopfKennungen(File.ReadAllText(datei)));

        Zeile("");
        Zeile("=== Nur-Lesen-Schutz ===");
        foreach (var p in schutz.Protokoll) Zeile("  " + p);
        Zeile($"  Abgewiesene Aufrufe: {schutz.Abgewiesen}");

        await File.WriteAllTextAsync(Path.Combine(ausgabe, "BERICHT.txt"), bericht.ToString(), new UTF8Encoding(false));
        return 0;
    }

    /// <summary>Skalare Kopfwerte des Datenobjekts (z. B. objectKeyValue) — dort steht die Kennung der Massnahme.</summary>
    private static string KopfKennungen(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() < 2) return "unerwartete Form";
            var daten = doc.RootElement[1];
            if (daten.ValueKind != JsonValueKind.Object) return "unerwartete Form";
            var werte = daten.EnumerateObject()
                .Where(p => p.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                .Select(p => $"{p.Name}={p.Value}");
            return string.Join(", ", werte);
        }
        catch (JsonException) { return "kein JSON"; }
    }

    /// <summary>Prüft den Filter an echten Adressformen, bevor irgendein Aufruf das Netz erreicht.</summary>
    private static bool SchutzHaelt()
    {
        const string editor = "https://www.geohost.ch/svc/rest/services/tn_system/gnsvc2023/MapServer/exts/GEONISserver2023/attributeeditor/";
        const string syn = "https://www.geohost.ch/divum/synserver;jsessionid=ABC";
        var mussAbgewiesen = new (HttpMethod, string, string)[]
        {
            (HttpMethod.Post, editor + "saveData?project=awu_abw&table=awk_haltung", "jsonobject=%7B%7D"),
            (HttpMethod.Get, editor + "saveData?project=awu_abw&table=awk_haltung", ""),
            (HttpMethod.Post, editor + "getLayoutDataCombined?table=awk_haltung", ""),
            (HttpMethod.Get, editor + "deleteData?table=awk_haltung", ""),
            (HttpMethod.Post, syn, "client=corejs&query=request_id%3D1%7Cserveraction%3DSAVE"),
            (HttpMethod.Put, editor + "getLayoutDataCombined", ""),
        };
        var mussDurch = new (HttpMethod, string, string)[]
        {
            (HttpMethod.Get, editor + "getLayoutDataCombined?project=awu_abw_edit&table=awk_haltung&id=X", ""),
            (HttpMethod.Get, editor + "getEmptyData?table=AWZ_UNTERHALT", ""),
            (HttpMethod.Get, editor + "getControlValues?table=AWZ_UNTERHALT", ""),
            (HttpMethod.Post, syn, "client=corejs&query=" + Uri.EscapeDataString("request_id=1|serveraction=GET_QUERY_FULL_TEXT|value=x")),
            (HttpMethod.Post, syn, "client=corejs&query=" + Uri.EscapeDataString("request_id=1|serveraction=GET_RESULTS|fidset=1")),
        };
        return mussAbgewiesen.All(f => NurLesenHandler.PruefeLesend(f.Item1, f.Item2, f.Item3) is not null)
               && mussDurch.All(f => NurLesenHandler.PruefeLesend(f.Item1, f.Item2, f.Item3) is null);
    }

    private static string? Wert(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private sealed record Einstellungen(string Basis, string Projekt, string Datenquelle, string? Login, string? Rollen, string? Gruppen);

    /// <summary>Liest (nur lesend) die WebGIS-Adresse und den gemerkten Benutzerkontext aus den SewerStudio-Einstellungen.</summary>
    private static Einstellungen LiesEinstellungen()
    {
        var pfad = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SewerStudio", "settings.json");
        string? Text(JsonElement r, string k) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(pfad));
            var r = doc.RootElement;
            return new Einstellungen(
                Text(r, "WebGisBasisUrl") ?? "https://www.geohost.ch",
                Text(r, "WebGisProjekt") ?? "awu_abw_edit",
                Text(r, "WebGisDatenquelle") ?? "awu_abw",
                Text(r, "WebGisSynLogin"), Text(r, "WebGisSynRoles"), Text(r, "WebGisSynGroups"));
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new Einstellungen("https://www.geohost.ch", "awu_abw_edit", "awu_abw", null, null, null);
        }
    }
}
