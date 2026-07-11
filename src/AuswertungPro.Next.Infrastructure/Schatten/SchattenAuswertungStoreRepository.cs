using System;
using System.IO;
using System.Text.Json;
using AuswertungPro.Next.Application.Schatten;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.Infrastructure.Schatten;

/// <summary>
/// Persistenz der Schattenauswertung: &lt;Projektordner&gt;/schatten/schatten_auswertung.json.
/// Exakt das costs.json-Muster: atomares Schreiben (temp + Replace + .bak) und ein
/// Lesefehler-Signal, bei dem der Aufrufer NICHT speichern darf (Audit 2026-06-12, K3 —
/// sonst ueberschreibt ein leerer Store eine nur gesperrte/beschaedigte Datei endgueltig).
/// </summary>
public sealed class SchattenAuswertungStoreRepository : ISchattenAuswertungStore
{
    private const string Ordner = "schatten";
    private const string Datei = "schatten_auswertung.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public SchattenAuswertungStore Load(string? projectPath, out string? loadError)
    {
        loadError = null;
        var path = ResolveStorePath(projectPath);
        if (path is null || !File.Exists(path))
            return new SchattenAuswertungStore();

        try
        {
            var json = File.ReadAllText(path);
            var store = JsonSerializer.Deserialize<SchattenAuswertungStore>(json, JsonOptions)
                ?? new SchattenAuswertungStore();
            return Normalize(store);
        }
        catch (JsonException ex)
        {
            loadError = $"{Datei} ist beschaedigt: {ex.Message}";
            return new SchattenAuswertungStore();
        }
        catch (Exception ex)
        {
            loadError = $"{Datei} konnte nicht gelesen werden (Datei evtl. gesperrt): {ex.Message}";
            return new SchattenAuswertungStore();
        }
    }

    public bool Save(string? projectPath, SchattenAuswertungStore store, out string error)
    {
        error = "";
        var path = ResolveStorePath(projectPath);
        if (path is null)
        {
            error = "Kein Projektpfad — Schattenauswertung kann nicht gespeichert werden.";
            return false;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = JsonSerializer.Serialize(store, JsonOptions);
            AtomicJsonFileWriter.WriteAllText(path, json);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string? ResolveStorePath(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            return null;
        var dir = Path.GetDirectoryName(projectPath);
        if (string.IsNullOrWhiteSpace(dir))
            return null;
        return Path.Combine(dir, Ordner, Datei);
    }

    private static SchattenAuswertungStore Normalize(SchattenAuswertungStore store)
    {
        // Case-insensitive Haltungs-Keys erzwingen (JSON-Deserialisierung liefert Ordinal).
        var normalized = new SchattenAuswertungStore
        {
            Version = store.Version > 0 ? store.Version : 1,
            LetzterLaufUtc = store.LetzterLaufUtc,
            KiModell = store.KiModell
        };
        foreach (var kvp in store.ByHaltung)
        {
            var key = kvp.Key?.Trim() ?? "";
            if (key.Length > 0 && kvp.Value is not null)
                normalized.ByHaltung[key] = kvp.Value;
        }
        return normalized;
    }
}
