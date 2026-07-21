using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Ai.Training.Inventory;

/// <summary>
/// Erstellt ein rein lesendes Inventar der lokalen Trainingsquellen.
/// Quelldateien und gespeicherte Pfade werden dabei niemals veraendert.
/// </summary>
public interface ITrainingDataInventoryService
{
    Task<TrainingDataInventoryReport> InspectAsync(
        TrainingDataInventoryRequest request,
        IProgress<TrainingDataInventoryProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fuehrt denselben Scan aus, liefert fuer den Export aber zusaetzlich die
    /// exakt dabei gelesenen Quelldatensaetze und Schutzlisten im Arbeitsspeicher.
    /// Es wird keine zweite Inventardatei angelegt.
    /// </summary>
    Task<TrainingDataInventoryRuntimeSnapshot> InspectRuntimeSnapshotAsync(
        TrainingDataInventoryRequest request,
        IProgress<TrainingDataInventoryProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed record TrainingDataInventoryRequest
{
    public required string KnowledgeRoot { get; init; }
    public string? EvalSetRoot { get; init; }
    public IReadOnlyList<string> SearchRoots { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ProtectedRoots { get; init; } = Array.Empty<string>();
    /// <summary>
    /// Explizit im Exportregister erwartete Schutz-Sets. Key ist die stabile
    /// Set-ID, Value der lokale Set-Ordner. Leer bedeutet den bisherigen
    /// EvalSetRoot-Entdeckungsweg.
    /// </summary>
    public IReadOnlyDictionary<string, string> ProtectedSetRoots { get; init; }
        = new Dictionary<string, string>();
    public bool IncludeBackups { get; init; } = true;
    public bool ComputeAssetHashes { get; init; } = true;
}

public sealed record TrainingDataInventoryProgress(
    string Stage,
    int Processed,
    int? Total = null);
