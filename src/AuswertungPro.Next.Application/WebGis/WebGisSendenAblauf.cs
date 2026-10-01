using System;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>
/// Das Änderungslog eines Schreiblaufs. <see cref="Schreibe"/> wirft <see cref="WebGisLogException"/>,
/// wenn die Zeile nicht dauerhaft steht: Das Log ist der Beleg, was im WebGIS verändert wurde.
/// </summary>
public interface IWebGisLaufLog
{
    /// <summary>Kennung dieses Laufs; steht auf jeder Zeile.</summary>
    string LaufId { get; }

    /// <summary>Schreibt eine oder mehrere Zeilen; wirft <see cref="WebGisLogException"/> bei einem Fehler.</summary>
    void Schreibe(string zeilen);

    /// <summary>Wie <see cref="Schreibe"/>, aber ohne Ausnahme; true, wenn die Zeilen stehen.</summary>
    bool VersucheSchreibe(string zeilen);
}

/// <summary>Das Änderungslog konnte nicht geschrieben werden.</summary>
public sealed class WebGisLogException : Exception
{
    public WebGisLogException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>Das Projekt, für das geschrieben wird, ist nicht mehr offen.</summary>
public sealed class WebGisProjektGewechseltException : Exception
{
    public WebGisProjektGewechseltException()
        : base("Das Projekt wurde gewechselt oder geschlossen — nichts mehr ins WebGIS geschrieben.") { }
}

/// <summary>
/// Ein echter Schreiblauf ins WebGIS mit Beleg (Plan WG03/WG04, Prüfung 28.09.2026):
/// <list type="bullet">
/// <item>Ohne geschriebene Startzeile geht kein einziger Aufruf ans WebGIS.</item>
/// <item>Vor jedem Schreibversuch steht der geplante Schritt im Log, danach das Ergebnis.</item>
/// <item>Fällt das Log während des Laufs aus, stoppt der Lauf vor dem nächsten Schreiben.</item>
/// <item>Vor jedem Schreibversuch wird geprüft, dass noch dasselbe Projekt offen ist.</item>
/// </list>
/// Was der Server bereits bestätigt hat, bleibt bestätigt — ein späterer Logfehler macht es nicht
/// zu «nicht geschrieben».
/// </summary>
public static class WebGisSendenAblauf
{
    public enum Ausgang
    {
        /// <summary>Alle schreibbaren Positionen wurden versucht, das Log ist vollständig.</summary>
        Abgeschlossen,
        /// <summary>Die Startzeile liess sich nicht schreiben — nichts wurde gesendet.</summary>
        KeinStartbeleg,
        /// <summary>Das Log fiel während des Laufs aus — der Lauf wurde gestoppt.</summary>
        LogAusgefallen,
        /// <summary>Das Projekt war nicht mehr offen — der Lauf wurde vor dem nächsten Schreiben gestoppt.</summary>
        ProjektGewechselt,
        /// <summary>
        /// Eine Sanierungsmassnahme wurde vom Server bestätigt, die Gegenprobe ergab aber keinen sicheren Befund —
        /// der Lauf wurde danach gestoppt (WG05). Vor einem neuen Versuch im WebGIS nachsehen.
        /// </summary>
        Ungeklaert,
    }

    public static async Task<Ausgang> FuehreAusAsync(
        WebGisExportUseCase useCase, WebGisExportPlan plan, IWebGisLaufLog log, string benutzer,
        Func<bool> projektGebunden, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(useCase);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(projektGebunden);

        if (!projektGebunden()) return Ausgang.ProjektGewechselt;
        if (!log.VersucheSchreibe(WebGisExportBericht.LogStart(DateTime.Now, benutzer, plan)))
            return Ausgang.KeinStartbeleg;

        void Vorher(string zeile)
        {
            if (!projektGebunden()) throw new WebGisProjektGewechseltException();
            Nachher(zeile);
        }

        void Nachher(string zeile)
        {
            if (zeile.Length > 0) log.Schreibe(zeile);
        }

        try
        {
            await useCase.FuehreAusAsync(plan, probelauf: false, ct,
                nachObjekt: p => Nachher(WebGisExportBericht.LogZeile(p, DateTime.Now)),
                nachMassnahme: s => Nachher(WebGisExportBericht.LogZeile(s, DateTime.Now)),
                vorObjekt: p => Vorher(WebGisExportBericht.LogGeplant(p, DateTime.Now)),
                vorMassnahme: s => Vorher(WebGisExportBericht.LogGeplant(s, DateTime.Now))).ConfigureAwait(false);
        }
        catch (WebGisLogException ex)
        {
            log.VersucheSchreibe(WebGisExportBericht.LogAbbruch(DateTime.Now, "Log nicht schreibbar: " + ex.Message, plan));
            return Ausgang.LogAusgefallen;
        }
        catch (WebGisProjektGewechseltException ex)
        {
            log.VersucheSchreibe(WebGisExportBericht.LogAbbruch(DateTime.Now, ex.Message, plan));
            return Ausgang.ProjektGewechselt;
        }
        catch (Exception ex)
        {
            log.VersucheSchreibe(WebGisExportBericht.LogAbbruch(DateTime.Now, ex.Message, plan));
            throw;
        }

        if (plan.Sanierungen.Exists(s => s.Ausgang == WebGisSchreibAusgang.Ungeklaert))
        {
            log.VersucheSchreibe(WebGisExportBericht.LogAbbruch(DateTime.Now,
                "Ausgang einer Sanierungsmassnahme ungeklärt — Lauf gestoppt, vor einem neuen Versuch im WebGIS nachsehen", plan));
            return Ausgang.Ungeklaert;
        }

        return log.VersucheSchreibe(WebGisExportBericht.LogAbschluss(DateTime.Now, plan))
            ? Ausgang.Abgeschlossen
            : Ausgang.LogAusgefallen;
    }

    /// <summary>
    /// Was nach einem Schreiblauf gemeldet wird (Status, Toast, Ergebnisfenster, Berichtsname). Bis 30.09.2026 stand
    /// diese Zuordnung in der Export-Seite (Wartbarkeitsaudit, WG-E); hier ist sie ohne Oberflaeche pruefbar.
    /// </summary>
    /// <param name="Ergebnis">Kurzer Ergebnistext (<see cref="WebGisExportBericht.Ergebnis"/>), Text des gruenen Toasts.</param>
    /// <param name="Zusatz">«ACHTUNG»-Zeile bei einem gestoppten Lauf, sonst leer.</param>
    /// <param name="Fehlgeschlagen">Warn-Toast statt Erfolg: Lauf nicht abgeschlossen, ein Fehler oder ein ungeklaerter Ausgang.</param>
    /// <param name="Berichtsart">«Ergebnis» nach vollstaendigem Lauf, sonst «Ergebnis-abgebrochen».</param>
    public sealed record Meldung(string Ergebnis, string Zusatz, bool Fehlgeschlagen, string Berichtsart)
    {
        /// <summary>Status und Text des Warn-Toasts.</summary>
        public string Text => Ergebnis + Zusatz;

        /// <summary>Text fuer das Ergebnisfenster: Ergebnis, Log-Datei samt Lauf und, falls geschrieben, der Bericht.</summary>
        public string Ergebnisfenster(string logPfad, string laufId, string? berichtPfad)
            => Text + $"\nLog: {logPfad} (Lauf {laufId})" + (berichtPfad is null ? "" : $"\nBericht: {berichtPfad}");
    }

    /// <summary>Status und Warn-Toast, wenn die Startzeile des Logs fehlt (<see cref="Ausgang.KeinStartbeleg"/>).</summary>
    public static string KeinStartbelegText(string logPfad)
        => "Das Änderungslog ist nicht schreibbar (" + logPfad + ") — nichts ins WebGIS geschrieben.";

    /// <summary>
    /// Ordnet einem Ausgang (nicht <see cref="Ausgang.KeinStartbeleg"/>, dort gilt <see cref="KeinStartbelegText"/>)
    /// die Meldung zu. «Fehlgeschlagen» liest den Schreibausgang jeder Position (<see cref="WebGisSchreibAusgang"/>).
    /// </summary>
    public static Meldung Melde(Ausgang ausgang, WebGisExportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var zusatz = ausgang switch
        {
            Ausgang.LogAusgefallen =>
                "\nACHTUNG: Das Änderungslog fiel während des Laufs aus — Lauf gestoppt. Bereits bestätigte "
                + "Änderungen stehen im Bericht; vor einem neuen Versuch im WebGIS nachsehen.",
            Ausgang.ProjektGewechselt =>
                "\nACHTUNG: Das Projekt war nicht mehr offen — Lauf vor dem nächsten Schreiben gestoppt.",
            Ausgang.Ungeklaert =>
                "\nACHTUNG: Eine Sanierungsmassnahme wurde vom Server bestätigt, liess sich aber nicht sicher nachprüfen — "
                + "Lauf gestoppt. Vor einem neuen Versuch im WebGIS nachsehen, nicht erneut anlegen.",
            _ => string.Empty,
        };
        var fehlgeschlagen = ausgang != Ausgang.Abgeschlossen
            || plan.Positionen.Exists(p => p.Ausgang == WebGisSchreibAusgang.Fehler)
            || plan.Sanierungen.Exists(s => s.Ausgang is WebGisSchreibAusgang.Fehler or WebGisSchreibAusgang.Ungeklaert);
        return new Meldung(WebGisExportBericht.Ergebnis(plan), zusatz, fehlgeschlagen,
            ausgang == Ausgang.Abgeschlossen ? "Ergebnis" : "Ergebnis-abgebrochen");
    }
}
