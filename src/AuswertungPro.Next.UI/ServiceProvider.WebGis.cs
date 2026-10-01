using System;
using System.Net;
using System.Net.Http;
using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.Infrastructure.WebGis;

namespace AuswertungPro.Next.UI
{
    public sealed partial class ServiceProvider
    {
        private IGeonisWebGisClient? _webGisClient;
        private HttpClient? _webGisHttp;
        private CookieContainer? _webGisCookies;
        private WebGisZugang? _webGisZugang;
        private PlaywrightWebGisAnmeldung? _webGisAnmeldung;

        /// <summary>
        /// Die aktuelle, angemeldete WebGIS-Sitzung. Wird gesetzt, nachdem sich der Benutzer
        /// im sichtbaren Browser (<see cref="WebGisAnmeldung"/>) an der echten WebGIS-Seite
        /// angemeldet hat (JSESSIONID + WebOffice-Kontext + Cookies aus dieser Sitzung).
        /// Solange null, liefert der Client keine Objekte (alle Positionen bleiben gesperrt).
        /// </summary>
        public WebGisZugang? WebGisZugang
        {
            get => _webGisZugang;
            set
            {
                _webGisZugang = value;
                UebernimmWebGisCookies(value);
            }
        }

        /// <summary>Sitzungsquelle: sichtbarer Playwright-Chromium, Anmeldung durch den Benutzer.</summary>
        public PlaywrightWebGisAnmeldung WebGisAnmeldung => _webGisAnmeldung ??= new PlaywrightWebGisAnmeldung();

        /// <summary>Vertrag der Sitzungsquelle (fuer die Registrierung).</summary>
        public IWebGisZugangQuelle WebGisZugangQuelle => WebGisAnmeldung;

        /// <summary>
        /// Client fuer den GEONIS-Attributeditor. Ein eigener HttpClient mit
        /// Cookie-Behaelter, damit die WebOffice-Session ueber alle Aufrufe traegt.
        /// </summary>
        public IGeonisWebGisClient WebGisClient
        {
            get
            {
                if (_webGisClient is null)
                {
                    _webGisCookies = new CookieContainer();
                    _webGisHttp = new HttpClient(new HttpClientHandler
                    {
                        CookieContainer = _webGisCookies,
                        UseCookies = true,
                        AllowAutoRedirect = true,
                    })
                    {
                        Timeout = TimeSpan.FromSeconds(60),
                    };
                    UebernimmWebGisCookies(_webGisZugang);
                    _webGisClient = new GeonisWebGisClient(_webGisHttp, () => WebGisZugang);
                }
                return _webGisClient;
            }
        }

        /// <summary>Der Export-Ablauf (Plan bauen, Vorschau, schreiben) ueber den Client.</summary>
        public WebGisExportUseCase WebGisExport => new(WebGisClient);

        /// <summary>Das Holen WebGIS -> SewerStudio (Felder, Laenge, Baujahr, Sanierungsmassnahmen); schreibt nie ins WebGIS.</summary>
        public WebGisImportUseCase WebGisImport => new(WebGisClient);

        private Services.WebGisHolenAblauf? _webGisHolen;

        /// <summary>«Vom WebGIS holen» fuer Haltungen, Schaechte und Export-Seite — eine Instanz, ein laufender Vorgang.</summary>
        public Services.WebGisHolenAblauf WebGisHolen => _webGisHolen ??= new Services.WebGisHolenAblauf(
            () => WebGisZugang is not null, () => WebGisImport, Dialogs);

        /// <summary>Browser-Cookies der Anmeldung in den HttpClient uebernehmen (JSESSIONID, ADFS …).</summary>
        private void UebernimmWebGisCookies(WebGisZugang? zugang)
        {
            if (_webGisCookies is null || zugang is null) return;
            foreach (var c in zugang.Cookies)
            {
                try
                {
                    var domain = c.Domain.TrimStart('.');
                    _webGisCookies.Add(new Cookie(c.Name, c.Wert, string.IsNullOrEmpty(c.Pfad) ? "/" : c.Pfad, domain));
                }
                catch (CookieException)
                {
                    // Einzelnes ungueltiges Cookie (z.B. exotischer Name) — die Sitzung traegt
                    // die JSESSIONID ohnehin auch als Query-Parameter.
                }
            }
        }
    }
}
