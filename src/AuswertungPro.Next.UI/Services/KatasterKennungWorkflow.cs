using System;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases;

namespace AuswertungPro.Next.UI.Services;

/// <summary>Was der Lauf getan hat — fuer die Statuszeile der Seite.</summary>
public sealed record KatasterKennungErgebnis(bool Ausgefuehrt, int Bauteile, string Meldung);

/// <summary>
/// Der Ablauf hinter "Katasterkennungen ergänzen": lesen, planen, Bericht zeigen,
/// nach Bestaetigung schreiben. Fuer Haltungen und Schaechte derselbe; was sich
/// unterscheidet, kommt ueber die zwei Delegaten herein.
///
/// Die Entscheidungen liegen in <see cref="KatasterKennungPlanBuilder"/> und
/// <see cref="KatasterKennungAnwender"/>; dieser Ablauf haelt nur Meldungen und
/// die Rueckfrage.
/// </summary>
public static class KatasterKennungWorkflow
{
    public static KatasterKennungErgebnis Fuehre(
        BauteilArt art,
        IKatasterKennungLeser leser,
        IDialogService dialogs,
        Func<KatasterKennungBestand, KatasterKennungPlan> plane,
        Func<KatasterKennungPlan, int> wendeAn)
    {
        ArgumentNullException.ThrowIfNull(leser);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(plane);
        ArgumentNullException.ThrowIfNull(wendeAn);

        var titel = art == BauteilArt.Haltung
            ? "Katasterkennungen ergänzen — Haltungen"
            : "Katasterkennungen ergänzen — Schächte";

        KatasterKennungBestand bestand;
        try
        {
            bestand = leser.Lies(art);
        }
        catch (Exception ex)
        {
            // Eine fehlende oder unlesbare Datei darf nie wie "nichts gefunden"
            // aussehen — sonst haelt der Benutzer eine Stoerung fuer eine Datenluecke.
            dialogs.Error(
                $"Die Kennungstabelle konnte nicht gelesen werden.\n\n{ex.Message}\n\n" +
                $"Eingestellte Datei:\n{leser.Quellpfad()}",
                titel);
            return new KatasterKennungErgebnis(false, 0, "Kennungstabelle nicht lesbar.");
        }

        var plan = plane(bestand);
        var bericht = KatasterKennungBericht.Schreibe(plan, leser.Quellpfad());

        if (plan.OhneAenderung)
        {
            dialogs.Info(bericht, titel);
            return new KatasterKennungErgebnis(false, 0, "Keine Kennungen zu übernehmen.");
        }

        if (dialogs.ConfirmCancel($"{bericht}\n\nDie Kennungen jetzt übernehmen?", titel)
            != DialogConfirm.Yes)
        {
            return new KatasterKennungErgebnis(false, 0, "Abgebrochen.");
        }

        var geschrieben = wendeAn(plan);
        return new KatasterKennungErgebnis(
            true,
            geschrieben,
            $"GEONIS-Kennungen für {geschrieben} Bauteile übernommen. Nicht vergessen zu speichern.");
    }
}
