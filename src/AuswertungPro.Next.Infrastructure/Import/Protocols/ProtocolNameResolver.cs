using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.Import.Protocols;

/// <summary>Art eines Protokolls: Haltung oder Schacht.</summary>
public enum ProtocolKind { Haltung, Schacht }

/// <summary>Zielangabe eines Protokoll-PDFs: Art + (unnormalisierter) Name.</summary>
public readonly record struct ProtocolTarget(ProtocolKind Kind, string Name);

/// <summary>
/// Ermittelt aus einem PDF-Pfad narrensicher (nur über Datei-/Ordnername, ohne PDF-Inhalt) die Art
/// (Haltung/Schacht) und den Namen. Reiner Helfer → unit-testbar. Nicht-Protokolle (Pläne, Listen,
/// Statistiken, orto/AV) werden übersprungen (null).
/// </summary>
public static class ProtocolNameResolver
{
    // Nicht-Protokolle: an diesen Namensbestandteilen erkennbar (klein geschrieben).
    // "plan" deckt Uebersichts-/Situations-/Lage-/Katasterplan usw. ab.
    private static readonly string[] NichtProtokoll =
        { "plan", "haltungsliste", "statistik", "_orto", "_av", "uebersicht", "übersicht" };

    public static ProtocolTarget? Resolve(string pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
            return null;

        var file = Path.GetFileNameWithoutExtension(pdfPath);
        var lowerFull = Path.GetFileName(pdfPath).ToLowerInvariant();
        if (NichtProtokoll.Any(p => lowerFull.Contains(p)))
            return null;

        // Name bereinigen: führendes YYYYMMDD_, Präfixe H_/L_/S_, Duplikat-Suffix _<ziffern>.
        var name = StripDatePrefix(file);
        var prefix = DetectPrefix(name);
        name = StripPrefix(name, prefix);
        name = StripDupSuffix(name).Trim();

        // Nur echte Haltungs-/Schacht-Ids akzeptieren (Ziffern, Punkte, Bindestriche). Verhindert,
        // dass alphanumerische Nicht-Protokolle (z.B. "Katasterplan_Zone3") als Schacht angelegt werden.
        if (!Regex.IsMatch(name, @"^\d[\d.\-]*$"))
            return null;

        // Art bestimmen: 1) Kategorie-Ordner (Haltungen/Schächte) IRGENDWO im Pfad — im aufgeteilten
        // Baum liegt die Datei unter <Kategorie>\<Id>\..., der Kategorie-Ordner ist also nicht der
        // direkte Elternordner. 2) Präfix H_/L_/S_. 3) '-'-Heuristik (Haltung = zwei Schächte).
        var kind = KindFromAncestors(pdfPath)
            ?? prefix switch
            {
                "H_" or "L_" => ProtocolKind.Haltung,
                "S_" => ProtocolKind.Schacht,
                _ => name.Contains('-') ? ProtocolKind.Haltung : ProtocolKind.Schacht
            };

        return new ProtocolTarget(kind, name);
    }

    // Läuft alle Ordner über der Datei ab und meldet die Kategorie, sobald ein Ordner
    // „Haltungen" bzw. „Schächte"/„Schaechte" heisst. Rein (nur Pfad-Stringoperationen).
    private static ProtocolKind? KindFromAncestors(string pdfPath)
    {
        var dir = Path.GetDirectoryName(pdfPath);
        while (!string.IsNullOrEmpty(dir))
        {
            var seg = Path.GetFileName(dir);
            if (seg.Equals("Haltungen", StringComparison.OrdinalIgnoreCase))
                return ProtocolKind.Haltung;
            if (seg.Equals("Schächte", StringComparison.OrdinalIgnoreCase) ||
                seg.Equals("Schaechte", StringComparison.OrdinalIgnoreCase))
                return ProtocolKind.Schacht;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static string StripDatePrefix(string s)
    {
        // "20260427_27581" -> "27581"
        var us = s.IndexOf('_');
        if (us == 8 && s[..8].All(char.IsDigit))
            return s[(us + 1)..];
        return s;
    }

    private static string? DetectPrefix(string s)
    {
        if (s.StartsWith("H_", StringComparison.OrdinalIgnoreCase)) return "H_";
        if (s.StartsWith("L_", StringComparison.OrdinalIgnoreCase)) return "L_";
        if (s.StartsWith("S_", StringComparison.OrdinalIgnoreCase)) return "S_";
        return null;
    }

    private static string StripPrefix(string s, string? prefix)
        => prefix is null ? s : s[prefix.Length..];

    private static string StripDupSuffix(string s)
    {
        // "<basis>_1" -> "<basis>" (nur wenn Suffix rein numerisch)
        var us = s.LastIndexOf('_');
        if (us > 0 && us < s.Length - 1 && s[(us + 1)..].All(char.IsDigit))
            return s[..us];
        return s;
    }
}
