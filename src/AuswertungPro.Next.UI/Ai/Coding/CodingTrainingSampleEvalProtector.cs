using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed class CodingTrainingSampleEvalProtector
{
    private readonly Func<EvalContaminationSets> _loadSets;
    private readonly Action<string> _log;
    private EvalContaminationSets? _sets;
    private bool _loadFailed;

    public CodingTrainingSampleEvalProtector(AppSettings? settings)
        : this(
            () => EvalContaminationSetProvider.Load(settings),
            message => BestEffort.ReportWarning(message))
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

    public bool IsProtected(byte[]? frameBytes, string? caseId)
    {
        var sets = GetSets();
        if (_loadFailed)
            return true;
        if (EvalContaminationGuard.IsEvalHaltung(sets.HaltungKeys, caseId))
            return true;
        if (frameBytes is null || frameBytes.Length == 0 || sets.ImageHashes.Count == 0)
            return false;

        var hash = Convert.ToHexStringLower(SHA256.HashData(frameBytes));
        return sets.ImageHashes.Contains(hash);
    }

    public EvalContaminationGuard.ExportContaminationResult Classify(TrainingSample sample)
    {
        var sets = GetSets();
        if (_loadFailed)
        {
            // Das bestehende Ergebnis-Enum kennt keinen Infrastrukturfehler.
            // Jeder Wert ausser Clean sperrt sicher; das Log enthaelt den echten Grund.
            return EvalContaminationGuard.ExportContaminationResult.EvalHaltung;
        }
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
            _loadFailed = true;
            _sets = new EvalContaminationSets(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        return _sets;
    }
}
