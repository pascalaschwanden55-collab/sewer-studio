using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Costs;

public sealed class ProjectCostStoreRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public ProjectCostStore Load(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            return new ProjectCostStore();

        var dir = Path.GetDirectoryName(projectPath);
        if (string.IsNullOrWhiteSpace(dir))
            return new ProjectCostStore();

        var path = GetStorePath(dir);
        if (!File.Exists(path))
            return new ProjectCostStore();

        try
        {
            var json = File.ReadAllText(path);
            var store = JsonSerializer.Deserialize<ProjectCostStore>(json, JsonOptions) ?? new ProjectCostStore();
            return Normalize(store);
        }
        catch
        {
            return new ProjectCostStore();
        }
    }

    public bool Save(string? projectPath, ProjectCostStore store, out string? error)
    {
        error = null;
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
            Directory.CreateDirectory(folder);
            var path = GetStorePath(dir);
            var json = JsonSerializer.Serialize(store, JsonOptions);
            File.WriteAllText(path, json);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static string GetStorePath(string projectDir)
        => Path.Combine(projectDir, "costs", "costs.json");

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
