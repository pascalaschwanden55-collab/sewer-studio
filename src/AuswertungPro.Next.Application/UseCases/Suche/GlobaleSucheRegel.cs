using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Suche;

public enum GlobaleSucheArt
{
    Haltung,
    Schacht,
    Strasse,
    /// <summary>Aufgabe 14 (Befehle in der Strg+K-Suche): ein ausfuehrbarer Befehl.</summary>
    Befehl,
    /// <summary>Nur von der UI eingefuegter Gruppenkopf «Befehle» — kein echter Treffer, nie
    /// auswaehlbar. Die reine Regel liefert ihn nie selbst; das haelt sie frei von
    /// Darstellungsfragen.</summary>
    Gruppenkopf
}

/// <summary>Ein Treffer der globalen Suche. <see cref="Glyph"/> ist nur bei Befehlen gesetzt
/// (Segoe-Fluent-Codepunkt fuer <c>ui:FluentIcon</c>); Daten-Treffer zeigen keins.</summary>
public sealed record GlobaleSucheTreffer(GlobaleSucheArt Art, string Text, object? Ziel, string? Glyph = null);

/// <summary>
/// Ein anbietbarer Befehl fuer die globale Suche (Aufgabe 14). <see cref="Verfuegbar"/> spiegelt
/// den tatsaechlichen <c>CanExecute</c>-Zustand des zugehoerigen ShellViewModel-Befehls bzw. die
/// Verfuegbarkeit der Zielseite — die Regel filtert danach, ein Befehl ohne offenes Projekt bleibt
/// unsichtbar statt nur deaktiviert. <see cref="Ausfuehren"/> ruft direkt den vorhandenen
/// ShellViewModel-Befehl bzw. die Navigation auf; die Regel selbst fuehrt nichts aus.
/// </summary>
public sealed record GlobaleSucheBefehlEintrag(
    string Schluessel,
    string Anzeigename,
    string Glyph,
    bool Verfuegbar,
    Action Ausfuehren);

/// <summary>
/// Inventar 8.5 / Aufgabe 14: Treffer Haltung, Schacht, Strasse (hoechstens 12) und seit
/// Aufgabe 14 zusaetzlich Befehle (hoechstens 6). Reine Regel, WPF-frei.
/// </summary>
public static class GlobaleSucheRegel
{
    public static IReadOnlyList<GlobaleSucheTreffer> Suche(
        string? text,
        IEnumerable<HaltungRecord> haltungen,
        IEnumerable<SchachtRecord> schaechte,
        Func<SchachtRecord, string> schachtNummer,
        IEnumerable<GlobaleSucheBefehlEintrag>? befehle = null,
        int maxDaten = 12,
        int maxBefehle = 6)
    {
        var q = (text ?? string.Empty).Trim();
        if (q.Length == 0)
            return Array.Empty<GlobaleSucheTreffer>();

        var daten = SucheDaten(q, haltungen, schaechte, schachtNummer, maxDaten);
        var befehlsTreffer = SucheBefehle(q, befehle, maxBefehle);

        // Sieht die Eingabe wie eine Nummer/einen Haltungsnamen aus (enthaelt Ziffern), bleiben
        // Datentreffer zuerst — so wie vor Aufgabe 14. Sonst gehen gut treffende Befehle voran,
        // weil ein reiner Wortsuchtext meistens einen Befehl meint ("neu", "einstellungen").
        return WirktWieNummer(q) || befehlsTreffer.Count == 0
            ? daten.Concat(befehlsTreffer).ToList()
            : befehlsTreffer.Concat(daten).ToList();
    }

    private static bool WirktWieNummer(string text) => text.Any(char.IsDigit);

    private static List<GlobaleSucheTreffer> SucheDaten(
        string q,
        IEnumerable<HaltungRecord> haltungen,
        IEnumerable<SchachtRecord> schaechte,
        Func<SchachtRecord, string> schachtNummer,
        int max)
    {
        bool Passt(string? s) => !string.IsNullOrEmpty(s) && s.Contains(q, StringComparison.OrdinalIgnoreCase);

        var ergebnis = new List<GlobaleSucheTreffer>();
        var strassen = new List<string>();
        foreach (var h in haltungen)
        {
            var name = h.GetFieldValue(FieldKeys.HoldingName);
            var strasse = h.GetFieldValue(FieldKeys.Street);
            if (Passt(name) || Passt(strasse))
                ergebnis.Add(new(GlobaleSucheArt.Haltung, string.IsNullOrWhiteSpace(strasse) ? $"Haltung {name}" : $"Haltung {name} · {strasse}", h));
            if (Passt(strasse) && !strassen.Contains(strasse!, StringComparer.OrdinalIgnoreCase))
                strassen.Add(strasse!);
        }
        foreach (var s in schaechte)
        {
            var nummer = schachtNummer(s);
            var strasse = s.GetFieldValue(FieldKeys.Street);
            if (Passt(nummer) || Passt(strasse))
                ergebnis.Add(new(GlobaleSucheArt.Schacht, string.IsNullOrWhiteSpace(strasse) ? $"Schacht {nummer}" : $"Schacht {nummer} · {strasse}", s));
            if (Passt(strasse) && !strassen.Contains(strasse!, StringComparer.OrdinalIgnoreCase))
                strassen.Add(strasse!);
        }
        ergebnis.AddRange(strassen.Select(st => new GlobaleSucheTreffer(GlobaleSucheArt.Strasse, $"Strasse {st}", st)));
        return ergebnis.Take(max).ToList();
    }

    private static List<GlobaleSucheTreffer> SucheBefehle(
        string q,
        IEnumerable<GlobaleSucheBefehlEintrag>? befehle,
        int max)
    {
        if (befehle is null)
            return new List<GlobaleSucheTreffer>();

        return befehle
            .Where(b => b.Verfuegbar && SucheTextFaltung.PasstAlle(q, new[] { b.Anzeigename }))
            .Take(max)
            .Select(b => new GlobaleSucheTreffer(GlobaleSucheArt.Befehl, b.Anzeigename, b.Ausfuehren, b.Glyph))
            .ToList();
    }
}
