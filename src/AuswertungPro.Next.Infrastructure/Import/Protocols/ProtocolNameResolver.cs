using System;
using System.IO;
using System.Linq;

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
    private static readonly string[] NichtProtokoll =
        { "übersichtsplan", "uebersichtsplan", "ubersichtsplan", "haltungsliste",
          "statistik", "_orto", "_av", "uebersicht", "übersicht" };

    public static ProtocolTarget? Resolve(string pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
            return null;

        var file = Path.GetFileNameWithoutExtension(pdfPath);
        var lowerFull = Path.GetFileName(pdfPath).ToLowerInvariant();
        if (NichtProtokoll.Any(p => lowerFull.Contains(p)))
            return null;

        var parent = new DirectoryInfo(Path.GetDirectoryName(pdfPath) ?? "").Name;

        // Name bereinigen: führendes YYYYMMDD_, Präfixe H_/L_/S_, Duplikat-Suffix _<ziffern>.
        var name = StripDatePrefix(file);
        var prefix = DetectPrefix(name);
        name = StripPrefix(name, prefix);
        name = StripDupSuffix(name).Trim();

        if (name.Length == 0 || !name.Any(char.IsDigit))
            return null; // sieht nicht nach Haltungs-/Schacht-Id aus

        // Art bestimmen: 1) Elternordner, 2) Präfix, 3) '-'-Heuristik.
        ProtocolKind kind;
        if (parent.Equals("Haltungen", StringComparison.OrdinalIgnoreCase))
            kind = ProtocolKind.Haltung;
        else if (parent.Equals("Schächte", StringComparison.OrdinalIgnoreCase) ||
                 parent.Equals("Schaechte", StringComparison.OrdinalIgnoreCase))
            kind = ProtocolKind.Schacht;
        else if (prefix is "H_" or "L_")
            kind = ProtocolKind.Haltung;
        else if (prefix is "S_")
            kind = ProtocolKind.Schacht;
        else
            kind = name.Contains('-') ? ProtocolKind.Haltung : ProtocolKind.Schacht;

        return new ProtocolTarget(kind, name);
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
