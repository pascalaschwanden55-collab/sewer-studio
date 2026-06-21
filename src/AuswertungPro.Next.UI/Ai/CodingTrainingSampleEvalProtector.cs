using System;
using System.Collections.Generic;
using System.Diagnostics;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai;

public sealed class CodingTrainingSampleEvalProtector
{
    private readonly Func<EvalContaminationSets> _loadSets;
    private readonly Action<string> _log;
    private EvalContaminationSets? _sets;

    public CodingTrainingSampleEvalProtector(AppSettings? settings)
        : this(
            () => EvalContaminationSetProvider.Load(settings),
            message => Debug.WriteLine(message))
    {
    }

    public CodingTrainingSampleEvalProtector(
        Func<EvalContaminationSets> loadSets,
        Action<string>? log = null)
    {
        _loadSets = loadSets ?? throw new ArgumentNullException(nameof(loadSets));
        _log = log ?? (_ => { });
    }

    public bool IsProtected(TrainingSample sample)
        => Classify(sample) != EvalContaminationGuard.ExportContaminationResult.Clean;

    public EvalContaminationGuard.ExportContaminationResult Classify(TrainingSample sample)
    {
        var sets = GetSets();
        return EvalContaminationGuard.ClassifyForExport(
            sets.ImageHashes,
            sets.HaltungKeys,
            sample.FramePath,
            sample.CaseId);
    }

    private EvalContaminationSets GetSets()
    {
        if (_sets is not null)
            return _sets;

        try
        {
            _sets = _loadSets();
        }
        catch (Exception ex)
        {
            _log($"[Training] Eval-Set konnte nicht geladen werden: {ex.Message}");
            _sets = new EvalContaminationSets(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        return _sets;
    }
}
