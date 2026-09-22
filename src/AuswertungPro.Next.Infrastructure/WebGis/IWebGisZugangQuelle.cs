namespace AuswertungPro.Next.Infrastructure.WebGis;

/// <summary>
/// Liefert die aktuelle, angemeldete WebOffice-Sitzung fuer den Client.
///
/// Bewusst KEIN nachgebauter HTTP-Login: Die WebOffice-Anmeldung ist ein eigener,
/// aenderbarer Ablauf (ADFS/Passwort). Robust und ohne Passwort im Programm ist, die echte
/// WebGIS-Seite in einem sichtbaren Browser anzumelden und danach die JSESSIONID (Cookie),
/// den X-syn-Kontext und die synserver-session_id aus dieser Sitzung zu lesen. Genau diese
/// Sitzung nutzt dann <see cref="GeonisWebGisClient"/> — dieselbe, die auch der Browser
/// verwendet.
///
/// Umsetzung: <see cref="PlaywrightWebGisAnmeldung"/> steuert den auf dem Rechner bereits
/// vorhandenen Edge (sonst Chrome, sonst Playwright-Chromium) mit einem eigenen, dauerhaften
/// Profil — kein WebView2-Paket, keine neue Abhaengigkeit, im Normalfall kein Download.
/// </summary>
public interface IWebGisZugangQuelle
{
    /// <summary>Die aktuelle Sitzung, oder null wenn (noch) nicht angemeldet.</summary>
    WebGisZugang? AktuellerZugang { get; }
}
