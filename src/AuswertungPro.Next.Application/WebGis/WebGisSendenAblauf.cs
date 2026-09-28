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

        return log.VersucheSchreibe(WebGisExportBericht.LogAbschluss(DateTime.Now, plan))
            ? Ausgang.Abgeschlossen
            : Ausgang.LogAusgefallen;
    }
}
