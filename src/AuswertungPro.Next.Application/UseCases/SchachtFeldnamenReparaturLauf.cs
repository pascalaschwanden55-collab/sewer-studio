using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases;

/// <summary>Was das Aufraeumen der Feldnamen tun wuerde.</summary>
public sealed record FeldnamenReparaturPlan(
    IReadOnlyDictionary<SchachtRecord, IReadOnlyList<FeldnamenGruppe>> JeSchacht,
    int GeprueteSchaechte)
{
    public IEnumerable<FeldnamenGruppe> Gruppen => JeSchacht.Values.SelectMany(g => g);

    public int ZusammenzufuehrendeSchreibweisen
        => Gruppen.Where(g => !g.Uneindeutig).Sum(g => g.Aufzuloesen.Count);

    public int UneindeutigeGruppen => Gruppen.Count(g => g.Uneindeutig);

    public int BetroffeneSchaechte
        => JeSchacht.Count(e => e.Value.Any(g => !g.Uneindeutig));

    public bool OhneAenderung => ZusammenzufuehrendeSchreibweisen == 0;

    /// <summary>Die Zielnamen mit der Zahl der jeweils aufgeloesten Schreibweisen.</summary>
    public IReadOnlyList<KeyValuePair<string, int>> JeZiel
        => Gruppen
            .Where(g => !g.Uneindeutig)
            .GroupBy(g => g.Ziel, StringComparer.Ordinal)
            .OrderByDescending(g => g.Sum(x => x.Aufzuloesen.Count))
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new KeyValuePair<string, int>(g.Key, g.Sum(x => x.Aufzuloesen.Count)))
            .ToList();

    /// <summary>Die unklaren Faelle, nach Zielname zusammengefasst.</summary>
    public IReadOnlyList<KeyValuePair<string, int>> UneindeutigJeZiel
        => Gruppen
            .Where(g => g.Uneindeutig)
            .GroupBy(g => g.Ziel, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .Select(g => new KeyValuePair<string, int>(g.Key, g.Count()))
            .ToList();
}

/// <summary>
/// Fuehrt doppelte Schreibweisen eines Schachtfeldes zusammen — erst als Plan,
/// dann nach Bestaetigung.
///
/// Schachtfelder heissen nach der Kopfzeile der Excel-Vorlage. Wurde die einmal mit
/// der falschen Zeichentabelle gelesen, entsteht ein zweites Feld mit kaputtem Namen.
/// Gemessen am Projekt Jagdmatt: 40 Schaechte tragen 150 solche Gruppen, darunter
/// "Ausführung Datum/Jahr" in sieben und "Primäre Schäden" in vier Schreibweisen.
///
/// Reine Rechnung; das Schreiben passiert erst in <see cref="Wende"/>.
/// </summary>
public static class SchachtFeldnamenReparaturLauf
{
    public static FeldnamenReparaturPlan Plane(
        IEnumerable<SchachtRecord> schaechte, IReadOnlyCollection<string>? spalten = null)
    {
        ArgumentNullException.ThrowIfNull(schaechte);

        var jeSchacht = new Dictionary<SchachtRecord, IReadOnlyList<FeldnamenGruppe>>();
        var geprueft = 0;

        foreach (var record in schaechte)
        {
            geprueft++;
            var gruppen = SchachtFeldnamenReparatur.Plane(record, spalten);
            if (gruppen.Count > 0)
                jeSchacht[record] = gruppen;
        }

        return new FeldnamenReparaturPlan(jeSchacht, geprueft);
    }

    /// <summary>Wendet den Plan an und liefert die Zahl der entfernten Schreibweisen.</summary>
    public static int Wende(FeldnamenReparaturPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return plan.JeSchacht.Sum(eintrag => SchachtFeldnamenReparatur.Wende(eintrag.Key, eintrag.Value));
    }

    public static string Bericht(FeldnamenReparaturPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var text = new StringBuilder();
        text.AppendLine($"Geprueft: {plan.GeprueteSchaechte} Schaechte im Projekt.");
        text.AppendLine();

        if (plan.OhneAenderung)
        {
            text.AppendLine("Es gibt nichts zusammenzufuehren — jedes Feld steht genau einmal da.");
        }
        else
        {
            text.AppendLine(
                $"{plan.ZusammenzufuehrendeSchreibweisen} doppelte Schreibweisen auf " +
                $"{plan.BetroffeneSchaechte} Schaechten wuerden zusammengefuehrt:");
            foreach (var (ziel, anzahl) in plan.JeZiel)
                text.AppendLine($"    {anzahl,6}x  ->  {Einzeilig(ziel)}");
        }

        if (plan.UneindeutigeGruppen > 0)
        {
            text.AppendLine();
            text.AppendLine(
                $"Nicht angefasst: {plan.UneindeutigeGruppen} Faelle, in denen zwei Schreibweisen");
            text.AppendLine("VERSCHIEDENE Werte tragen. Welcher gilt, kann nur der Mensch entscheiden.");
            foreach (var (ziel, anzahl) in plan.UneindeutigJeZiel)
                text.AppendLine($"    {anzahl,6}x  {Einzeilig(ziel)}");
        }

        text.AppendLine();
        text.AppendLine("Werte gehen dabei nicht verloren: Der gefuellte Wert wandert in den");
        text.AppendLine("bleibenden Namen, nur die leere Zweitschreibweise verschwindet.");

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// Ein Feldname fuer den Bericht. Die Kopfzeile der Vorlage traegt
    /// Zeilenumbrueche ("Status\noffen/abgeschlossen"); im Dialog wuerden sie die
    /// Aufzaehlung zerreissen.
    /// </summary>
    private static string Einzeilig(string name)
        => name.Replace("\r", " ", StringComparison.Ordinal)
               .Replace("\n", " ", StringComparison.Ordinal)
               .Trim();
}
