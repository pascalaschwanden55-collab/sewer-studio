using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Audit 2026-05-06 Top-10: Frames-Cleanup-Job. Der frames/-Ordner unter
/// C:\KI_BRAIN waechst auf ~67 GB ohne dass alte/verwaiste PNGs jemals
/// geloescht werden. Dieser Service identifiziert Frames die zu keinem
/// TrainingSample mehr gehoeren und kann sie loeschen.
///
/// Sicherheits-Default: <see cref="DryRun"/> = true → Service meldet nur,
/// loescht nichts. Aufrufer setzt explizit DryRun = false fuer den
/// realen Cleanup-Lauf.
/// </summary>
public sealed class FrameStoreCleanupService
{
    private readonly Func<Task<IReadOnlyCollection<string>>> _loadActiveSampleIds;

    /// <summary>
    /// Standard-Konstruktor: nutzt <see cref="TrainingSamplesStore.LoadAsync"/>
    /// und extrahiert SampleIds aus den Samples (jeder Sample hat einen Frame
    /// unter frames/{SampleId}.png).
    /// </summary>
    public FrameStoreCleanupService()
    {
        _loadActiveSampleIds = async () =>
        {
            var samples = await TrainingSamplesStore.LoadAsync().ConfigureAwait(false);
            return samples.Select(s => s.SampleId)
                          .Where(id => !string.IsNullOrEmpty(id))
                          .ToHashSet(StringComparer.OrdinalIgnoreCase);
        };
    }

    /// <summary>Test-Hook: Aufrufer kann eigene SampleId-Quelle uebergeben.</summary>
    public FrameStoreCleanupService(Func<Task<IReadOnlyCollection<string>>> loadActiveSampleIds)
    {
        _loadActiveSampleIds = loadActiveSampleIds ?? throw new ArgumentNullException(nameof(loadActiveSampleIds));
    }

    /// <summary>
    /// Wenn true (Default), werden Verwaiste nur gemeldet, nicht geloescht.
    /// Auf false setzen fuer den realen Cleanup-Lauf.
    /// </summary>
    public bool DryRun { get; set; } = true;

    /// <summary>
    /// Mindestalter in Tagen — Frames juenger als das werden geschont, auch
    /// wenn sie keinen Sample-Bezug haben (Zwischenstand bei laufenden
    /// Imports). Default 7 Tage.
    /// </summary>
    public int MinimumAgeDays { get; set; } = 7;

    /// <summary>
    /// Scannt den frames/-Ordner, identifiziert Verwaiste und (wenn DryRun=false)
    /// loescht sie. Gibt eine Zusammenfassung zurueck.
    /// </summary>
    public async Task<FrameStoreCleanupResult> RunAsync(CancellationToken ct = default)
    {
        var framesDir = FrameStore.GetFramesDir();
        if (!Directory.Exists(framesDir))
        {
            return new FrameStoreCleanupResult(
                FramesDir: framesDir,
                TotalFiles: 0,
                ActiveSampleIds: 0,
                OrphanFiles: 0,
                OrphanBytes: 0,
                DeletedFiles: 0,
                DeletedBytes: 0,
                DryRun: DryRun,
                Errors: Array.Empty<string>());
        }

        var activeIds = await _loadActiveSampleIds().ConfigureAwait(false);
        var allFiles = Directory.EnumerateFiles(framesDir, "*.png", SearchOption.TopDirectoryOnly).ToList();
        var cutoff = DateTime.UtcNow.AddDays(-MinimumAgeDays);

        long orphanBytes = 0;
        var orphans = new List<FileInfo>();
        foreach (var path in allFiles)
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileNameWithoutExtension(path);
            if (activeIds.Contains(name)) continue;

            var info = new FileInfo(path);
            if (info.LastWriteTimeUtc > cutoff) continue; // zu jung — schonen

            orphans.Add(info);
            orphanBytes += info.Length;
        }

        long deletedBytes = 0;
        int deletedFiles = 0;
        var errors = new List<string>();

        if (!DryRun)
        {
            foreach (var info in orphans)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    info.Delete();
                    deletedFiles++;
                    deletedBytes += info.Length;
                }
                catch (Exception ex)
                {
                    errors.Add($"{info.Name}: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        return new FrameStoreCleanupResult(
            FramesDir: framesDir,
            TotalFiles: allFiles.Count,
            ActiveSampleIds: activeIds.Count,
            OrphanFiles: orphans.Count,
            OrphanBytes: orphanBytes,
            DeletedFiles: deletedFiles,
            DeletedBytes: deletedBytes,
            DryRun: DryRun,
            Errors: errors);
    }
}

/// <summary>Zusammenfassung des Frame-Cleanup-Laufs.</summary>
public sealed record FrameStoreCleanupResult(
    string FramesDir,
    int TotalFiles,
    int ActiveSampleIds,
    int OrphanFiles,
    long OrphanBytes,
    int DeletedFiles,
    long DeletedBytes,
    bool DryRun,
    IReadOnlyList<string> Errors);
