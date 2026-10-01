using System.Collections.Generic;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Settings;

/// <summary>
/// Reiner Textabgleich fuer die Einstellungssuche. Umlaute und ihre ae/oe/ue-Schreibweise
/// gelten als gleich. Mehrere Suchwoerter muessen alle vorkommen. Die Faltung selbst liegt
/// seit Aufgabe 14 (Befehle in der Strg+K-Suche) in <see cref="SucheTextFaltung"/>
/// (Application, WPF-frei), damit die globale Suche dieselbe Regel verwendet.
/// </summary>
public static class SettingsSearchMatcher
{
    public static string Normalisiere(string text) => SucheTextFaltung.Falte(text);

    public static bool Passt(string suche, IEnumerable<string> texte) => SucheTextFaltung.PasstAlle(suche, texte);
}
