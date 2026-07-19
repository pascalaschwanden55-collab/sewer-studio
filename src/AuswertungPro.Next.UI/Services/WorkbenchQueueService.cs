using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Liefert die zwei Pruefplatz-Quellen (Etappe 1): lose Fotos und die Review-Warteschlange
/// aus dem Sample-Bestand. Filter- und Sortierlogik ist rein und testbar; das Fenster ruft nur.
/// </summary>
public sealed class WorkbenchQueueService
{
    private readonly ITrainingSampleStore _sampleStore;
    private readonly Func<string, bool> _fileExists;

    public WorkbenchQueueService(ITrainingSampleStore sampleStore, Func<string, bool>? fileExists = null)
    {
        _sampleStore = sampleStore;
        _fileExists = fileExists ?? File.Exists;
    }

    /// <summary>
    /// Baut aus gewaehlten Fotopfaden Pruefplatz-Items: CaseId <c>foto_yyyyMMdd_&lt;lfd&gt;</c>, Meter 0/0,
    /// keine Haltungsherkunft (bewusst QuarantineOrigin), optionaler Rohrdurchmesser.
    /// </summary>
    public static IReadOnlyList<WorkbenchItem> BuildPhotoItems(
        IReadOnlyList<string> photoPaths, DateTime now, int? pipeDiameterMm)
    {
        var stamp = now.ToString("yyyyMMdd");
        var items = new List<WorkbenchItem>(photoPaths.Count);
        for (var i = 0; i < photoPaths.Count; i++)
        {
            items.Add(new WorkbenchItem(
                photoPaths[i],
                $"foto_{stamp}_{i + 1}",
                MeterStart: 0, MeterEnd: 0,
                HaltungName: null, VideoPath: null,
                PipeDiameterMm: pipeDiameterMm));
        }

        return items;
    }

    /// <summary>Laedt die Review-Warteschlange aus dem Sample-Bestand.</summary>
    public async Task<IReadOnlyList<WorkbenchItem>> LoadReviewQueueAsync()
    {
        var samples = await _sampleStore.LoadAsync().ConfigureAwait(false);
        return BuildReviewQueue(samples, _fileExists);
    }

    /// <summary>
    /// Reine, testbare Auswahl: nur QualityGate Yellow/Red, noch nicht menschlich beurteilt
    /// (<see cref="TrainingSample.HumanConfirmed"/> == null) und mit vorhandener Bilddatei.
    /// Sortierung: Red vor Yellow, danach neueste Inspektion zuerst.
    /// </summary>
    public static IReadOnlyList<WorkbenchItem> BuildReviewQueue(
        IReadOnlyList<TrainingSample> samples, Func<string, bool> fileExists)
        => samples
            .Where(s => IsReviewCandidate(s, fileExists))
            .OrderByDescending(s => GateRank(s.QualityGateLevel))
            .ThenByDescending(s => s.InspectionDate ?? DateTime.MinValue)
            .Select(ToItem)
            .ToList();

    private static bool IsReviewCandidate(TrainingSample s, Func<string, bool> fileExists)
        => GateRank(s.QualityGateLevel) > 0
           && s.HumanConfirmed is null
           && !string.IsNullOrWhiteSpace(s.FramePath)
           && fileExists(s.FramePath);

    private static int GateRank(string? gate)
        => string.Equals(gate, "Red", StringComparison.OrdinalIgnoreCase) ? 2
         : string.Equals(gate, "Yellow", StringComparison.OrdinalIgnoreCase) ? 1
         : 0;

    private static WorkbenchItem ToItem(TrainingSample s)
        => new(
            s.FramePath,
            s.CaseId,
            s.MeterStart,
            s.MeterEnd,
            // Review-Sample stammt aus dieser Haltung -> HaltungName setzen schliesst die QuarantineOrigin-Luecke.
            HaltungName: string.IsNullOrWhiteSpace(s.CaseId) ? null : s.CaseId,
            VideoPath: null,
            PipeDiameterMm: null);
}
