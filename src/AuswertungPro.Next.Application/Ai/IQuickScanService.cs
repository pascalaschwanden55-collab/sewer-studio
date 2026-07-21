using System;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// KI-Schnellscan eines Videos: liefert Segmente mit optionalem Schadenshinweis.
/// Der Vertrag haelt die UI vom direkten Aufbau des Ollama-Clients und der
/// Infrastruktur-Pipeline fern.
/// </summary>
public interface IQuickScanService
{
    Task<QuickScanResult> ScanAsync(
        string videoPath,
        IProgress<QuickScanProgress>? progress,
        CancellationToken ct);
}

/// <summary>
/// Kurzlebige Schnellscan-Sitzung: buendelt den fuer einen Lauf gebauten
/// <see cref="IQuickScanService"/> mit seinen Laufzeit-Ressourcen (u.a. dem
/// eigenen Ollama-HTTP-Client). Der Aufrufer haelt die Sitzung nur fuer die
/// Dauer eines Scans und gibt sie danach frei.
/// </summary>
public interface IQuickScanSession : IDisposable
{
    IQuickScanService Service { get; }
}
