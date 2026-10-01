using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace AuswertungPro.Next.Infrastructure.WebGis;

/// <summary>
/// Der bekannte WebOffice-Benutzerkontext (X-syn-*), den die Editor-Aufrufe mitschicken.
/// Er aendert sich je Benutzer praktisch nie und wird nach der ersten Anmeldung gemerkt,
/// damit spaeter die Anmeldung allein genuegt (ohne eine Attributmaske zu oeffnen).
/// </summary>
public sealed record WebGisSynKontext(string Login, string Roles, string Groups)
{
    /// <summary>
    /// Die Werte gehen als Kopfzeilen an jeden WebGIS-Aufruf (Pruefung 22.09.2026, D2): Login und Gruppen gefuellt,
    /// keine Steuerzeichen (ein Zeilenumbruch haengte eine fremde Kopfzeile an), hoechstens 2000 Zeichen je Wert.
    /// </summary>
    public bool IstPlausibel
        => !string.IsNullOrWhiteSpace(Login) && !string.IsNullOrWhiteSpace(Groups)
           && Sauber(Login) && Sauber(Roles) && Sauber(Groups);

    private static bool Sauber(string wert) => wert.Length <= 2000 && !wert.Any(char.IsControl);

    /// <summary>
    /// Der gemerkte Kontext aus den Einstellungen, geprueft wie ein frisch gelesener. Null ohne Login oder Gruppen
    /// oder wenn er nicht plausibel ist — dann wird er bei der Anmeldung neu erfasst.
    /// </summary>
    public static WebGisSynKontext? AusEinstellungen(string? login, string? roles, string? groups)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(groups)) return null;
        var kontext = new WebGisSynKontext(login, string.IsNullOrWhiteSpace(roles) ? "WebOffice+-+Editing" : roles, groups);
        return kontext.IstPlausibel ? kontext : null;
    }
}

/// <summary>
/// Sitzungsquelle ueber einen sichtbaren Playwright-Chromium: Der Benutzer meldet sich
/// selbst an der echten WebGIS-Seite an (ADFS/Passwort bleiben beim Browser, SewerStudio
/// sieht kein Passwort). Danach werden aus der Seite gelesen: jsessionid und die
/// synserver-session_id (scriptAPI.Env()), die Cookies, und aus dem ersten
/// Attributeditor-Aufruf der X-syn-Kontext (Login/Rollen/Gruppen).
///
/// Chromium kommt aus der vorhandenen Playwright-Installation (wie der PDF-Renderer);
/// kein neues Paket. Der Browser bleibt bis <see cref="SchliessenAsync"/> offen, damit
/// die Sitzung lebt, solange exportiert wird.
/// </summary>
public sealed class PlaywrightWebGisAnmeldung : IWebGisZugangQuelle, IAsyncDisposable
{
    private static readonly Regex SynLoginRx = new(@"X-syn-login=([^&]+)", RegexOptions.Compiled);
    private static readonly Regex SynRolesRx = new(@"X-syn-application-roles=([^&]+)", RegexOptions.Compiled);
    private static readonly Regex SynGroupsRx = new(@"X-syn-groups=([^&]+)", RegexOptions.Compiled);

    private IPlaywright? _playwright;
    private IBrowserContext? _context;
    private IPage? _page;
    private WebGisSynKontext? _erfasst;
    private string _basisUrl = "";

    public WebGisZugang? AktuellerZugang { get; private set; }

    /// <summary>Zuletzt erfasster oder uebergebener X-syn-Kontext (zum Merken in den Einstellungen).</summary>
    public WebGisSynKontext? SynKontext => _erfasst;

    /// <summary>
    /// Oeffnet das WebGIS im Browser und wartet, bis der Benutzer angemeldet ist und alle
    /// Sitzungswerte vorliegen. <paramref name="bekannterKontext"/>: gemerkter X-syn-Kontext;
    /// ohne ihn muss der Benutzer einmal eine Attributmaske oeffnen (der Aufruf traegt ihn).
    /// Null bei Abbruch, Zeitablauf oder geschlossenem Browser.
    /// </summary>
    public async Task<WebGisZugang?> AnmeldenAsync(
        string basisUrl, string projekt, string datenquelle,
        WebGisSynKontext? bekannterKontext, IProgress<string>? fortschritt = null,
        TimeSpan? zeitlimit = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basisUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(projekt);
        ArgumentException.ThrowIfNullOrWhiteSpace(datenquelle);

        await SchliessenAsync().ConfigureAwait(false);
        _basisUrl = basisUrl;
        _erfasst = bekannterKontext;
        AktuellerZugang = null;

        _playwright = await Playwright.CreateAsync().WaitAsync(ct).ConfigureAwait(false);
        _context = await OeffneBrowserAsync(fortschritt, ct).ConfigureAwait(false);
        _context.Request += (_, req) => Erfasse(req.Url);
        _page = _context.Pages.Count > 0 ? _context.Pages[0] : await _context.NewPageAsync().WaitAsync(ct).ConfigureAwait(false);

        var start = basisUrl.TrimEnd('/') + "/divum/synserver?project=" + Uri.EscapeDataString(projekt);
        await _page.GotoAsync(start, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60_000 })
            .WaitAsync(ct).ConfigureAwait(false);
        fortschritt?.Report("Bitte im Browser am WebGIS anmelden …");

        var ende = DateTime.UtcNow + (zeitlimit ?? TimeSpan.FromMinutes(10));
        var kontextHinweisGezeigt = false;
        while (DateTime.UtcNow < ende)
        {
            ct.ThrowIfCancellationRequested();
            if (_page.IsClosed) return null;

            var env = await LiesEnvAsync().ConfigureAwait(false);
            if (env is { Angemeldet: true, JSessionId: { Length: > 0 }, SessionId: { Length: > 0 } })
            {
                if (_erfasst is null)
                {
                    if (!kontextHinweisGezeigt)
                    {
                        fortschritt?.Report("Angemeldet. Bitte einmal eine Attributmaske öffnen (Objekt anklicken → Attributmaske), damit SewerStudio den Benutzerkontext lesen kann …");
                        kontextHinweisGezeigt = true;
                    }
                }
                else
                {
                    var cookies = await LiesCookiesAsync().ConfigureAwait(false);
                    AktuellerZugang = new WebGisZugang
                    {
                        BasisUrl = basisUrl.TrimEnd('/'),
                        Projekt = projekt,
                        Datenquelle = datenquelle,
                        JSessionId = env.JSessionId!,
                        SynSessionId = env.SessionId,
                        SynLogin = _erfasst.Login,
                        SynRoles = _erfasst.Roles,
                        SynGroups = _erfasst.Groups,
                        Cookies = cookies,
                    };
                    fortschritt?.Report($"Angemeldet als {_erfasst.Login}.");
                    return AktuellerZugang;
                }
            }
            await Task.Delay(1000, ct).ConfigureAwait(false);
        }
        return null;
    }

    /// <summary>Ordner des eigenen Browserprofils (Cookies bleiben zwischen den Anmeldungen bestehen).</summary>
    public static string ProfilOrdner => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SewerStudio", "WebGisBrowser");

    /// <summary>
    /// Oeffnet das sichtbare Browserfenster mit eigenem, dauerhaftem Profil.
    ///
    /// Reihenfolge (Entscheid Pascal 21.09.2026 — nichts Grosses herunterladen muessen):
    /// 1. Microsoft Edge, auf jedem Windows vorhanden — kein Download noetig.
    /// 2. Google Chrome, falls installiert.
    /// 3. Der Chromium von Playwright; fehlt er, wird er wie beim PDF-Export einmalig ueber
    ///    playwright.ps1 nach %LOCALAPPDATA%\ms-playwright geholt (rund 150 MB).
    ///
    /// Das Profil ist NICHT das Alltagsprofil des Benutzers: eigener Ordner, eigene Cookies.
    /// Dadurch bleibt die WebOffice-Anmeldung ueber Programmstarts hinweg erhalten und stoert
    /// zugleich kein offenes Browserfenster des Benutzers.
    /// </summary>
    private async Task<IBrowserContext> OeffneBrowserAsync(IProgress<string>? fortschritt, CancellationToken ct)
    {
        System.IO.Directory.CreateDirectory(ProfilOrdner);

        foreach (var kanal in new[] { "msedge", "chrome" })
        {
            try
            {
                return await StarteProfilAsync(kanal, ct).ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
                // Dieser Browser ist nicht installiert — naechsten versuchen.
            }
        }

        try
        {
            return await StarteProfilAsync(null, ct).ConfigureAwait(false);
        }
        catch (PlaywrightException ex) when (BrowserFehlt(ex))
        {
            fortschritt?.Report("Weder Edge noch Chrome gefunden — Chromium wird einmalig nachinstalliert (rund 150 MB) …");
            var (ok, meldung) = await InstalliereChromiumAsync(ct).ConfigureAwait(false);
            if (!ok)
                throw new InvalidOperationException(
                    "Kein Browser fuer die WebGIS-Anmeldung gefunden (Edge, Chrome oder Chromium). " + meldung
                    + " Manuell: in einer PowerShell im Programmordner von SewerStudio "
                    + "\"./playwright.ps1 install chromium\" ausfuehren.", ex);
            return await StarteProfilAsync(null, ct).ConfigureAwait(false);
        }
    }

    private async Task<IBrowserContext> StarteProfilAsync(string? kanal, CancellationToken ct)
    {
        var optionen = new BrowserTypeLaunchPersistentContextOptions
        {
            Headless = false,
            Timeout = 60_000,
            Channel = kanal,
            ViewportSize = new ViewportSize { Width = 1400, Height = 900 },
        };
        return await _playwright!.Chromium
            .LaunchPersistentContextAsync(ProfilOrdner, optionen)
            .WaitAsync(ct).ConfigureAwait(false);
    }

    private static bool BrowserFehlt(PlaywrightException ex)
    {
        var m = ex.Message;
        return m.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase)
            || m.Contains("playwright install", StringComparison.OrdinalIgnoreCase)
            || m.Contains("download new browsers", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(bool Ok, string Meldung)> InstalliereChromiumAsync(CancellationToken ct)
    {
        var basis = AppContext.BaseDirectory;
        var skript = System.IO.Path.Combine(basis, "playwright.ps1");
        if (!System.IO.File.Exists(skript))
            return (false, "playwright.ps1 liegt nicht im Programmordner.");

        foreach (var shell in new[] { "pwsh", "powershell" })
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = shell, WorkingDirectory = basis,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    UseShellExecute = false, CreateNoWindow = true,
                };
                foreach (var a in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", skript, "install", "chromium" })
                    psi.ArgumentList.Add(a);

                // Pruefung 22.09.2026, D3: Beide Ausgaben gleichzeitig lesen, mit Zeitlimit — wie beim PDF-Export. Vorher
                // las die Anmeldung nur die Fehlerausgabe; ein voller Puffer der normalen Ausgabe hielt das Skript an,
                // und SewerStudio wartete ewig. Der Download ist rund 150 MB, deshalb 10 Minuten.
                var ergebnis = await AuswertungPro.Next.Application.Common.ExternalProcessRunner
                    .RunAsync(psi, TimeSpan.FromMinutes(10), ct).ConfigureAwait(false);
                if (ergebnis.ExitCode == 0 && !ergebnis.TimedOut) return (true, "");
                return (false, ergebnis.TimedOut
                    ? $"{shell}: Installation nach 10 Minuten abgebrochen."
                    : $"{shell}: {ergebnis.Message ?? $"endete mit Code {ergebnis.ExitCode}."}");
            }
            catch (System.ComponentModel.Win32Exception) { /* Shell nicht vorhanden -> naechste */ }
        }
        return (false, "Weder pwsh noch powershell konnte gestartet werden.");
    }

    /// <summary>Liest jsessionid, synserver-sessionid und den Anmeldestatus aus der WebOffice-Seite.</summary>
    /// <summary>Was die WebOffice-Seite ueber die Sitzung sagt.</summary>
    private sealed record EnvStand(bool Angemeldet, string? JSessionId, string? SessionId);

    private async Task<EnvStand?> LiesEnvAsync()
    {
        if (_page is null || _page.IsClosed) return null;
        try
        {
            var json = await _page.EvaluateAsync<string?>(@"() => {
                try {
                    if (!window.scriptAPI && window.client && window.client.loadScriptAPI) window.client.loadScriptAPI();
                    if (!window.scriptAPI) return null;
                    const e = window.scriptAPI.Env();
                    return JSON.stringify({ j: e.jsessionid || null, s: e.sessionid || null, n: !!e.isumnameduser });
                } catch (err) { return null; }
            }").ConfigureAwait(false);
            if (string.IsNullOrEmpty(json)) return null;
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var r = doc.RootElement;
            return new EnvStand(
                r.TryGetProperty("n", out var n) && n.ValueKind == System.Text.Json.JsonValueKind.True,
                r.TryGetProperty("j", out var j) && j.ValueKind == System.Text.Json.JsonValueKind.String ? j.GetString() : null,
                r.TryGetProperty("s", out var s) && s.ValueKind == System.Text.Json.JsonValueKind.String ? s.GetString() : null);
        }
        catch (PlaywrightException)
        {
            return null; // Seite gerade im Wechsel (Login-Redirect) — naechster Versuch.
        }
    }

    private async Task<IReadOnlyList<WebGisCookie>> LiesCookiesAsync()
    {
        if (_context is null) return Array.Empty<WebGisCookie>();
        var liste = new List<WebGisCookie>();
        foreach (var c in await _context.CookiesAsync().ConfigureAwait(false))
            liste.Add(new WebGisCookie(c.Name, c.Value, c.Domain, c.Path));
        return liste;
    }

    /// <summary>Aus einem Editor-Aufruf (…?X-syn-login=…&amp;X-syn-groups=…) den Kontext lesen.</summary>
    internal void Erfasse(string url)
    {
        if (_erfasst is not null || string.IsNullOrEmpty(url)) return;
        var kontext = KontextAusUrl(url, _basisUrl);
        if (kontext is not null) _erfasst = kontext;
    }

    /// <summary>
    /// Den Kontext nur aus einer Anfrage an genau den WebGIS-Server und nur ueber HTTPS (Pruefung 22.09.2026, D2):
    /// Der Browser fragt auch fremde Server an (Karten, Skripte) — deren Adressen bestimmen nie den Benutzer.
    /// </summary>
    public static WebGisSynKontext? KontextAusUrl(string url, string basisUrl)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var adresse)
            || !Uri.TryCreate(basisUrl, UriKind.Absolute, out var basis)
            || adresse.Scheme != Uri.UriSchemeHttps
            || !string.Equals(adresse.Host, basis.Host, StringComparison.OrdinalIgnoreCase))
            return null;
        var login = SynLoginRx.Match(url);
        var groups = SynGroupsRx.Match(url);
        if (!login.Success || !groups.Success) return null;
        var roles = SynRolesRx.Match(url);
        var kontext = new WebGisSynKontext(
            Dekodiere(login.Groups[1].Value),
            roles.Success ? Dekodiere(roles.Groups[1].Value) : "WebOffice+-+Editing",
            Dekodiere(groups.Groups[1].Value));
        return kontext.IstPlausibel ? kontext : null;
    }

    /// <summary>
    /// Query-Wert einmal dekodieren. Der Rollenwert lautet woertlich "WebOffice+-+Editing"
    /// (die Plus-Zeichen gehoeren zum Wert; der Browser sendet %2B) — nicht zu Leerzeichen machen.
    /// </summary>
    private static string Dekodiere(string s) => Uri.UnescapeDataString(s);

    public async Task SchliessenAsync()
    {
        try { if (_context is not null) await _context.CloseAsync().ConfigureAwait(false); } catch (PlaywrightException) { /* Browser schon zu: nichts mehr zu schliessen. */ }
        _playwright?.Dispose();
        _page = null; _context = null; _playwright = null;
        AktuellerZugang = null;
    }

    public async ValueTask DisposeAsync() => await SchliessenAsync().ConfigureAwait(false);
}
