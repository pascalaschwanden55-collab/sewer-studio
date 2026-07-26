using System;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Backup;

/// <summary>
/// Hält den vollständigen Wissensordner auf einem externen Datenträger aktuell.
/// Die Quelle wird dabei niemals verändert.
/// </summary>
public interface IKnowledgeRealtimeMirrorService : IDisposable
{
    string SourceRoot { get; }

    string? TargetRoot { get; }

    bool IsRunning { get; }

    /// <summary>
    /// Startet den Hintergrundabgleich und die anschließende Dateiüberwachung.
    /// Mehrere Aufrufe sind erlaubt.
    /// </summary>
    void Start();

    /// <summary>
    /// Führt sofort einen vollständigen, inkrementellen Abgleich aus.
    /// </summary>
    Task SynchronizeNowAsync(CancellationToken ct = default);
}
