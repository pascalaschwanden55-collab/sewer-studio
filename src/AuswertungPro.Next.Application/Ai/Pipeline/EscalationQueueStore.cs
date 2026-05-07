using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Application.Ai.Pipeline;

/// <summary>
/// Persistiert Yellow/Red-Frames als JSONL fuer gebuendelte 32B-Eskalation.
/// Ueberlebt Programm-Abbruch — wird beim naechsten Start abgearbeitet.
/// Pfad: {KnowledgeRoot}/escalation_queue.jsonl (UI registriert KnowledgeRootProvider).
/// </summary>
public sealed class EscalationQueueStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _path;
    private readonly object _lock = new();

    public EscalationQueueStore()
    {
        _path = PathConstants.InKnowledgeRoot(PathConstants.EscalationQueueFile);
    }

    /// <summary>Frame zur Queue hinzufuegen (append, thread-safe).</summary>
    public void Enqueue(EscalationItem item)
    {
        var json = JsonSerializer.Serialize(item, JsonOpts);
        lock (_lock)
        {
            File.AppendAllText(_path, json + "\n");
        }
    }

    /// <summary>Alle pendenten Frames lesen (fuer 32B-Abarbeitung).</summary>
    public List<EscalationItem> ReadAll()
    {
        lock (_lock)
        {
            if (!File.Exists(_path))
                return [];

            var items = new List<EscalationItem>();
            foreach (var line in File.ReadAllLines(_path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var item = JsonSerializer.Deserialize<EscalationItem>(line, JsonOpts);
                    if (item != null) items.Add(item);
                }
                catch { /* Korrupte Zeile ueberspringen */ }
            }
            return items;
        }
    }

    /// <summary>Queue hat pendente Items.</summary>
    public bool HasPending()
    {
        lock (_lock)
        {
            return File.Exists(_path) && new FileInfo(_path).Length > 0;
        }
    }

    /// <summary>Anzahl pendenter Items.</summary>
    public int Count()
    {
        var items = ReadAll();
        return items.Count;
    }

    /// <summary>Queue leeren (nach erfolgreicher 32B-Abarbeitung).</summary>
    public void Clear()
    {
        lock (_lock)
        {
            if (File.Exists(_path))
                File.Delete(_path);
        }
    }
}
