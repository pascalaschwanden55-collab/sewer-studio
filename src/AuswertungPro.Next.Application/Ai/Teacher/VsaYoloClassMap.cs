using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Teacher;

namespace AuswertungPro.Next.Application.Ai.Teacher;

/// <summary>
/// Mapping von VSA-Codes auf YOLO-Klassen-IDs.
/// Auto-wachsend: Neue Codes bekommen automatisch die naechste freie ID.
/// Persistiert unter KnowledgeRoot als yolo_class_map.json.
///
/// Strategie: Exakte Codes fuer visuell unterscheidbare Klassen.
/// Grundgeruest-Codes (BCA, BCC, BCD, BCE) als eigene Klassen,
/// damit YOLO sie separat erkennen kann.
/// </summary>
public static class VsaYoloClassMap
{
    private static readonly object _lock = new();
    private static Dictionary<string, int>? _map;

    /// <summary>
    /// Standard-Mapping: Exakte Codes fuer Grundgeruest + haeufige Schadensklassen.
    /// Feinere Untercodes (z.B. BCAEB) werden auf ihren visuellen Hauptcode gemappt.
    /// </summary>
    private static readonly Dictionary<string, int> _defaults = new(StringComparer.OrdinalIgnoreCase)
    {
        // Grundgeruest der Haltung (visuell klar unterscheidbar)
        ["BCD"] = 0,   // Rohranfang (Schacht sichtbar)
        ["BCE"] = 1,   // Rohrende (Schacht sichtbar)
        ["BCA"] = 2,   // Seitlicher Anschluss (Rohroeffnung in Wand)
        ["BCC"] = 3,   // Bogen (Richtungsaenderung)

        // Strukturelle Schaeden
        ["BAB"] = 4,   // Riss (laengs, quer, etc.)
        ["BAC"] = 5,   // Bruch
        ["BAF"] = 6,   // Deformation
        ["BAH"] = 7,   // Versatz
        ["BAI"] = 8,   // Einragung Stutzen

        // Oberflaechenschaeden
        ["BAJ"] = 9,   // Korrosion / Ausbrueche
        ["BBB"] = 10,  // Anhaftende Stoffe (Inkrustation, Fett, Faeulnis)
        ["BBA"] = 11,  // Wurzeln (Pfahl, fein, komplex)

        // Betriebliche Stoerungen
        ["BBC"] = 12,  // Ablagerung Sohle
        ["BBD"] = 13,  // Eindringender Boden
        ["BDA"] = 14,  // Allgemeinzustand (Fotobeispiel)
    };

    private static string GetMapPath()
        => Path.Combine(KnowledgeRootProvider.GetRoot(), "yolo_class_map.json");

    /// <summary>
    /// Gibt die YOLO classId fuer einen VSA-Code zurueck.
    /// Reduziert auf 2-Zeichen-Hauptkategorie.
    /// Neue Kategorien bekommen automatisch die naechste freie ID.
    /// </summary>
    public static int GetClassId(string vsaCode)
    {
        if (string.IsNullOrWhiteSpace(vsaCode)) return 0;

        var category = ExtractCategory(vsaCode);

        lock (_lock)
        {
            EnsureLoaded();

            if (_map!.TryGetValue(category, out int id))
                return id;

            // Neue Kategorie: naechste freie ID vergeben
            int nextId = _map.Count > 0 ? _map.Values.Max() + 1 : 0;
            _map[category] = nextId;
            SaveSync();
            return nextId;
        }
    }

    /// <summary>
    /// V4.2: Umgekehrte Zuordnung class_id → VSA-Code (fuer EvalRunner + Report).
    /// Gibt ersten passenden Code zurueck, oder null wenn classId nicht im Mapping.
    /// </summary>
    public static string? GetVsaCodeForClassId(int classId)
    {
        lock (_lock)
        {
            EnsureLoaded();
            foreach (var kv in _map!)
            {
                if (kv.Value == classId) return kv.Key;
            }
            return null;
        }
    }

    /// <summary>
    /// Gibt das komplette Mapping als Dictionary zurueck (fuer YOLO classes.txt Export).
    /// </summary>
    public static Dictionary<string, int> GetFullMap()
    {
        lock (_lock)
        {
            EnsureLoaded();
            return new Dictionary<string, int>(_map!, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Exportiert classes.txt fuer YOLO-Training (eine Klasse pro Zeile, Index = Zeilennummer).
    /// </summary>
    public static async Task ExportClassesTxtAsync(string outputPath)
    {
        var map = GetFullMap();
        var lines = map
            .OrderBy(kv => kv.Value)
            .Select(kv => kv.Key)
            .ToArray();
        await File.WriteAllLinesAsync(outputPath, lines);
    }

    /// <summary>
    /// Extrahiert den visuellen Hauptcode aus einem VSA-Code.
    /// Untercodes werden auf ihren erkennbaren Hauptcode reduziert:
    /// "BCAEB" → "BCA" (Anschluss), "BCC.Y.B" → "BCC" (Bogen),
    /// "BABBA" → "BAB" (Riss), "BBCA" → "BBC" (Ablagerung).
    /// </summary>
    private static string ExtractCategory(string vsaCode)
    {
        var clean = vsaCode.Replace(".", "").Trim().ToUpperInvariant();
        if (clean.Length < 2) return clean;

        // Exakte Matches zuerst (Grundgeruest)
        if (clean.StartsWith("BCD")) return "BCD";
        if (clean.StartsWith("BCE")) return "BCE";
        if (clean.StartsWith("BCA")) return "BCA";
        if (clean.StartsWith("BCC")) return "BCC";

        // 3-Zeichen visueller Hauptcode fuer Schaeden
        if (clean.Length >= 3)
        {
            var prefix3 = clean[..3];
            if (_defaults.ContainsKey(prefix3))
                return prefix3;
        }

        // Fallback: unbekannter Code → als eigene Klasse (auto-wachsend)
        return clean.Length >= 3 ? clean[..3] : clean;
    }

    private static void EnsureLoaded()
    {
        if (_map != null) return;

        var path = GetMapPath();
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                _map = JsonSerializer.Deserialize<Dictionary<string, int>>(json)
                       ?? new Dictionary<string, int>(_defaults, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                _map = new Dictionary<string, int>(_defaults, StringComparer.OrdinalIgnoreCase);
            }
        }
        else
        {
            _map = new Dictionary<string, int>(_defaults, StringComparer.OrdinalIgnoreCase);
            SaveSync();
        }
    }

    private static void SaveSync()
    {
        try
        {
            var path = GetMapPath();
            var dir = Path.GetDirectoryName(path);
            if (dir != null) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_map, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);

            // Auto-Export classes.txt neben yolo_class_map.json (fuer YOLO-Training)
            var classesPath = Path.Combine(Path.GetDirectoryName(path)!, "classes.txt");
            var lines = _map!.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToArray();
            File.WriteAllLines(classesPath, lines);
        }
        catch
        {
            // Stilles Fehlschlagen — Mapping funktioniert im Speicher weiter
        }
    }
}
