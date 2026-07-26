using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Costs;

public sealed class ProjectCostStoreRepository : IProjectCostStoreRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    // Dateiname im costs-Ordner. Standard "costs.json" (Haltungen); die Schacht-Matrix nutzt
    // "schacht_costs.json", damit Schacht- und Haltungs-Keys sich nicht ueberschreiben.
    private readonly string _fileName;

    public ProjectCostStoreRepository(string fileName = "costs.json")
        => _fileName = string.IsNullOrWhiteSpace(fileName) ? "costs.json" : fileName.Trim();

    public ProjectCostStore Load(string? projectPath) => Load(projectPath, out _);

    /// <summary>
    /// Laedt den Store. loadError != null bedeutet: Datei existiert, konnte aber nicht
    /// gelesen werden (beschaedigt ODER gesperrt, z.B. Virenscanner/Cloud-Sync). Der
    /// Aufrufer darf dann NICHT speichern, sonst ueberschreibt der leere Store die
    /// echten Kostendaten endgueltig (Audit 2026-06-12, K3).
    /// </summary>
    public ProjectCostStore Load(string? projectPath, out string? loadError)
    {
        loadError = null;
        if (string.IsNullOrWhiteSpace(projectPath))
            return new ProjectCostStore();

        var dir = Path.GetDirectoryName(projectPath);
        if (string.IsNullOrWhiteSpace(dir))
            return new ProjectCostStore();

        var path = ResolveStorePath(dir);
        var probe = CostStoreFileProbe.Probe(path);
        if (probe.State == CostStorePathState.Missing)
            return new ProjectCostStore();
        if (probe.State == CostStorePathState.Invalid)
        {
            loadError =
                $"{_fileName} konnte nicht sicher gelesen werden: " +
                (probe.Error ?? "Dateipfad ist ungueltig.");
            return new ProjectCostStore();
        }

        try
        {
            var json = File.ReadAllText(path);
            var store = JsonSerializer.Deserialize<ProjectCostStore>(json, JsonOptions) ?? new ProjectCostStore();
            return Normalize(store);
        }
        catch (JsonException ex)
        {
            loadError = $"{_fileName} ist beschaedigt: {ex.Message}";
            return new ProjectCostStore();
        }
        catch (Exception ex)
        {
            loadError = $"{_fileName} konnte nicht gelesen werden (Datei evtl. gesperrt): {ex.Message}";
            return new ProjectCostStore();
        }
    }

    public bool Save(string? projectPath, ProjectCostStore store, out string? error)
    {
        error = null;
        if (store?.ByHolding is null)
        {
            error = "Kostendaten fehlen oder sind ungueltig; Speichern ist gesperrt.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(projectPath))
        {
            error = "Projektpfad fehlt.";
            return false;
        }

        var dir = Path.GetDirectoryName(projectPath);
        if (string.IsNullOrWhiteSpace(dir))
        {
            error = "Projektordner fehlt.";
            return false;
        }

        try
        {
            var folder = Path.Combine(dir, "costs");
            var path = ResolveStorePath(dir);
            var probe = CostStoreFileProbe.Probe(path);
            if (probe.State == CostStorePathState.Invalid)
            {
                error =
                    $"Speichern ist gesperrt: {_fileName} ist nicht sicher zugreifbar: " +
                    (probe.Error ?? "Dateipfad ist ungueltig.");
                return false;
            }

            if (probe.State == CostStorePathState.File)
            {
                _ = Load(projectPath, out var existingLoadError);
                if (!string.IsNullOrWhiteSpace(existingLoadError))
                {
                    error =
                        $"Speichern ist gesperrt: vorhandene {_fileName} konnte nicht " +
                        $"sicher geladen werden: {existingLoadError}";
                    return false;
                }
            }

            Directory.CreateDirectory(folder);
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

    /// <summary>Pfad des Standard-Haltungs-Stores (costs.json). Rueckwaertskompatibel.</summary>
    public static string GetStorePath(string projectDir)
        => Path.Combine(projectDir, "costs", "costs.json");

    string IProjectCostStoreRepository.GetStorePath(string projectDirectory)
        => ResolveStorePath(projectDirectory);

    // Pfad dieser Repo-Instanz (Haltungen: costs.json, Schaechte: schacht_costs.json).
    private string ResolveStorePath(string projectDir)
        => Path.Combine(projectDir, "costs", _fileName);

    private static ProjectCostStore Normalize(ProjectCostStore store)
    {
        var normalized = new ProjectCostStore
        {
            ByHolding = new Dictionary<string, HoldingCost>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var kvp in store.ByHolding)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
                continue;
            if (kvp.Value is null)
                continue;
            kvp.Value.Holding = string.IsNullOrWhiteSpace(kvp.Value.Holding) ? kvp.Key.Trim() : kvp.Value.Holding.Trim();
            normalized.ByHolding[kvp.Key.Trim()] = kvp.Value;
        }

        return normalized;
    }
}
