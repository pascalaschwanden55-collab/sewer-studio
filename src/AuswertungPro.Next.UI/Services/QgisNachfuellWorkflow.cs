using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Services;

/// <summary>Was der Nachfuelllauf getan hat — fuer die Statuszeile der Seite.</summary>
public sealed record QgisNachfuellErgebnis(bool Ausgefuehrt, int GeschriebeneFelder, string Meldung);

/// <summary>
/// Der Ablauf hinter "Leere Felder aus QGIS ergaenzen": lesen, planen, Bericht
/// zeigen, nach Bestaetigung schreiben.
///
/// Der Ablauf ist fuer Haltungen und Schaechte derselbe — deshalb steht er einmal
/// hier und nicht zweimal in den beiden Seiten. Was sich unterscheidet, kommt
/// ueber die zwei Delegaten herein.
///
/// Die Entscheidungen liegen in <see cref="LeereFelderPlanBuilder"/> und
/// <see cref="LeereFelderAnwender"/>; dieser Ablauf haelt nur Meldungen und die
/// Rueckfrage.
/// </summary>
public static class QgisNachfuellWorkflow
{
    public static QgisNachfuellErgebnis Fuehre(
        BauteilArt art,
        IQgisBestandLeser leser,
        IDialogService dialogs,
        Func<QgisBestand, LeereFelderPlan> plane,
        Func<LeereFelderPlan, int> wendeAn)
    {
        ArgumentNullException.ThrowIfNull(leser);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(plane);
        ArgumentNullException.ThrowIfNull(wendeAn);

        var titel = art == BauteilArt.Haltung
            ? "Leere Felder aus QGIS ergänzen — Haltungen"
            : "Leere Felder aus QGIS ergänzen — Schächte";

        QgisBestand bestand;
        try
        {
            bestand = leser.Lies(art);
        }
        catch (Exception ex)
        {
            // Eine fehlende oder unlesbare Datei darf nie wie "nichts gefunden"
            // aussehen — sonst haelt der Benutzer eine Stoerung fuer eine Datenluecke.
            dialogs.Error(
                $"Der QGIS-Bestand konnte nicht gelesen werden.\n\n{ex.Message}\n\n" +
                $"Eingestellte Datei:\n{leser.Quellpfad(art)}",
                titel);
            return new QgisNachfuellErgebnis(false, 0, "QGIS-Bestand nicht lesbar.");
        }

        var plan = plane(bestand);
        var bericht = LeereFelderBericht.Schreibe(plan, leser.Quellpfad(art));

        if (plan.OhneAenderung)
        {
            dialogs.Info(bericht, titel);
            return new QgisNachfuellErgebnis(false, 0, "Nichts zu ergänzen.");
        }

        if (dialogs.ConfirmCancel($"{bericht}\n\nDie leeren Felder jetzt ergänzen?", titel)
            != DialogConfirm.Yes)
        {
            return new QgisNachfuellErgebnis(false, 0, "Abgebrochen.");
        }

        var geschrieben = wendeAn(plan);
        return new QgisNachfuellErgebnis(
            true,
            geschrieben,
            $"{geschrieben} leere Felder aus QGIS ergänzt. Nicht vergessen zu speichern.");
    }
}
