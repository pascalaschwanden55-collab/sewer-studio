using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Gemeinsame Fehlereinordnung eines Sidecar-Aufrufs der Mehrmodell-Analyse (AP05b). Der Helfer
/// liefert nur die <see cref="MultiModelFehlerart"/>; was daraus folgt (Log- und Fortschrittstext,
/// Trace, Telemetrie, ob der Fehler das Bild beendet), entscheidet weiterhin jeder Modellschritt
/// selbst - die Regeln der Modelle bleiben bewusst getrennt.
/// Einordnung: Nutzerabbruch wird sofort weitergeworfen und nie gezaehlt; VRAM-Mangel
/// (<see cref="SidecarInsufficientVramException"/>) ist ein Kapazitaetsfehler; jede andere Ausnahme,
/// auch ein interner Zeitablauf, ist ein Transportfehler.
/// </summary>
internal static class MultiModelSidecarAufruf
{
    /// <summary>Antwort oder eingeordneter Fehler eines Aufrufs.</summary>
    internal readonly record struct Ausgang<T>(T? Antwort, MultiModelFehlerart Fehlerart, Exception? Fehler)
        where T : class;

    /// <summary>
    /// Fuehrt <paramref name="aufruf"/> aus. Alles, was der Aufruf selbst tut (auch eine Nachbearbeitung
    /// der Antwort), liegt innerhalb derselben Fehlereinordnung wie vor der Auslagerung.
    /// </summary>
    public static async Task<Ausgang<T>> AusfuehrenAsync<T>(Func<Task<T>> aufruf, CancellationToken ct)
        where T : class
    {
        try
        {
            return new Ausgang<T>(await aufruf().ConfigureAwait(false), MultiModelFehlerart.Keine, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Nutzerabbruch: sofort weiterwerfen, nie als Sidecar-Ausfall zaehlen.
            throw;
        }
        catch (SidecarInsufficientVramException ex)
        {
            // Paket 2/A4: VRAM-Mangel ist ein Kapazitaetsfehler, KEIN Transport-Ausfall.
            return new Ausgang<T>(null, MultiModelFehlerart.Kapazitaet, ex);
        }
        catch (Exception ex)
        {
            return new Ausgang<T>(null, MultiModelFehlerart.Transport, ex);
        }
    }
}
