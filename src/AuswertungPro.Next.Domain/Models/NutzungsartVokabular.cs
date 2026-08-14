using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Die Nutzungsart einer Haltung — mit den Begriffen der Norm, an einer Stelle.
///
/// Massgebend ist SIA405 2020, die Fassung der heutigen Datenlieferungen. Frueher fuehrte
/// das Programm eigene Kurzformen ("Schmutzwasser", "Regenwasser"), die es beim Import
/// aus der Norm uebersetzte. Beim Export entstanden dadurch Werte, die kein
/// INTERLIS-Pruefer akzeptiert. Deshalb gilt jetzt in der ganzen App der Normbegriff.
///
/// Beim Regenwasser widersprechen sich die Normen. Keine Fassung kennt den Wert der
/// anderen, deshalb entscheidet beim Schreiben die Fassung der Zieldatei:
/// <list type="bullet">
///   <item>SIA405 2020 (und die App): <c>Niederschlagsabwasser</c></item>
///   <item>SIA405 2015 und aelter: <c>Regenabwasser</c></item>
/// </list>
///
/// Reine Werte-Logik ohne Zustand und ohne Dateizugriff.
/// </summary>
public static class NutzungsartVokabular
{
    /// <summary>
    /// Ein fachliches Konzept mit allen Schreibweisen, die dafuer gelesen werden, und der
    /// jeweils gueltigen Ausgabe je Modellfassung.
    /// </summary>
    private sealed record Konzept(string[] Gelesen, string App, string Bis2015, string Ab2020);

    private static readonly Konzept[] Konzepte =
    [
        new(["schmutzabwasser", "schmutzwasser", "abwasser"],
            "Schmutzabwasser", "Schmutzabwasser", "Schmutzabwasser"),
        new(["niederschlagsabwasser", "regenabwasser", "regenwasser", "meteorwasser"],
            "Niederschlagsabwasser", "Regenabwasser", "Niederschlagsabwasser"),
        new(["mischabwasser", "mischwasser"],
            "Mischabwasser", "Mischabwasser", "Mischabwasser"),
        new(["entlastetes mischabwasser", "entlastetes_mischabwasser"],
            "entlastetes Mischabwasser", "entlastetes_Mischabwasser", "entlastetes_Mischabwasser"),
        new(["reinabwasser", "reinwasser"],
            "Reinabwasser", "Reinabwasser", "Reinabwasser"),
        new(["bachwasser"], "Bachwasser", "Bachwasser", "Bachwasser"),
        new(["industrieabwasser", "industriewasser"],
            "Industrieabwasser", "Industrieabwasser", "Industrieabwasser"),
        new(["andere"], "andere", "andere", "andere"),
        new(["unbekannt"], "unbekannt", "unbekannt", "unbekannt")
    ];

    /// <summary>
    /// Die Auswahl im Programm — leer plus die neun Werte aus SIA405, in der Reihenfolge
    /// der Norm. Neue Werte gehoeren in <see cref="Konzepte"/>, nicht in eine zweite Liste.
    /// </summary>
    public static readonly IReadOnlyList<string> Auswahl = new ReadOnlyCollection<string>(
        new[] { "" }
            .Concat(Konzepte.Select(k => k.App).OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
            .ToList());

    /// <summary>
    /// Bringt eine beliebige gelesene Schreibweise auf den Begriff der App.
    ///
    /// Bekannte Altwerte und Importschreibweisen werden erkannt. Ein unbekannter Wert
    /// bleibt unveraendert stehen: Er koennte eine Angabe enthalten, die niemand sonst
    /// kennt — sie zu loeschen waere schlimmer, als sie stehen zu lassen.
    /// </summary>
    public static string Normalisieren(string? wert)
    {
        var text = (wert ?? "").Trim();
        if (text.Length == 0)
            return "";

        return Finde(text)?.App ?? text;
    }

    /// <summary>
    /// Die im Zielmodell gueltige Schreibweise, oder <c>null</c>, wenn der Wert dort zu
    /// keinem gueltigen Begriff gehoert. Dann wird nichts geschrieben statt geraten.
    /// </summary>
    /// <param name="ab2020">
    /// <c>true</c> fuer SIA405 2020 und neuer, <c>false</c> fuer aeltere Fassungen,
    /// <c>null</c>, wenn die Fassung der Datei nicht erkennbar ist.
    /// </param>
    public static string? NachModell(string? wert, bool? ab2020)
    {
        var konzept = Finde((wert ?? "").Trim());
        if (konzept is null)
            return null;

        if (string.Equals(konzept.Bis2015, konzept.Ab2020, StringComparison.Ordinal))
            return konzept.Ab2020;

        // Nur hier entscheidet die Fassung — ohne sie waere jede Wahl geraten.
        return ab2020 switch
        {
            true => konzept.Ab2020,
            false => konzept.Bis2015,
            _ => null
        };
    }

    private static Konzept? Finde(string text)
    {
        if (text.Length == 0)
            return null;

        var klein = text.ToLowerInvariant();
        return Konzepte.FirstOrDefault(k => k.Gelesen.Contains(klein));
    }
}
