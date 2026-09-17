namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Die Fehlercodes des Projektladens und die eine Regel, welcher davon eine
/// Sicherung einspielen darf.
/// </summary>
public static class ProjektLadefehler
{
    /// <summary>Datei existiert nicht.</summary>
    public const string NichtGefunden = "APP-NOTFOUND";

    /// <summary>Inhalt ist belegt unbrauchbar (kaputtes JSON, JSON-null).</summary>
    public const string Inhalt = "APP-LOAD";

    /// <summary>Datei ist vorhanden und in Ordnung, aber gerade nicht lesbar.</summary>
    public const string Zugriff = "APP-ACCESS";

    /// <summary>Datei stammt aus einer neueren Programmversion.</summary>
    public const string Version = "APP-VERSION";

    /// <summary>
    /// Nur eine belegt unbrauchbare oder fehlende Datei darf durch eine Sicherung
    /// ersetzt werden. Alles andere - gesperrt, kein Zugriff, zu neues Format oder
    /// ein unbekannter Fehler - laesst den vorhandenen Stand unangetastet.
    /// </summary>
    public static bool DarfSicherungEinspielen(string? errorCode)
        => errorCode is Inhalt or NichtGefunden;
}
