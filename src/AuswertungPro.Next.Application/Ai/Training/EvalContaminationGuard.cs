using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Reine Pruef-Funktion gegen Eval-Set-Kontamination: verhindert, dass ein Bild aus dem
/// eingefrorenen Eval-Set ueber den KB-/FewShot-Indexpfad in die Trainings-/Retrieval-Daten
/// gelangt (sonst messen Benchmarks keine Generalisierung mehr).
///
/// Spiegelt die Hash-Logik des StageAExporter (SHA-256 als Hex, lowercase, inhaltsbasiert –
/// NICHT dateinamenbasiert), damit derselbe Eval-Hash-Satz wiederverwendet werden kann.
///
/// BEWUSST NICHT in KnowledgeBaseManager.IsIndexWorthy verdrahtet: das ist eine separate,
/// fachliche Betriebsentscheidung (blockieren vs. nur warnen) und gehoert in einen eigenen
/// Schritt. Hier nur die testbare, seiteneffektfreie Pruefung.
/// </summary>
public static class EvalContaminationGuard
{
    /// <summary>
    /// SHA-256-Hex (lowercase) des Datei-Inhalts; null wenn der Pfad leer ist oder die Datei fehlt.
    /// </summary>
    public static string? ComputeFileHash(string? framePath)
    {
        if (string.IsNullOrWhiteSpace(framePath) || !File.Exists(framePath))
            return null;

        using var stream = File.OpenRead(framePath);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>
    /// True, wenn das Bild unter <paramref name="framePath"/> inhaltsgleich zu einem
    /// Eval-Set-Bild ist (Hash-Vergleich gegen <paramref name="evalImageHashes"/>).
    /// Leerer Hash-Satz oder fehlende Datei -> false (kein falscher Alarm).
    /// </summary>
    public static bool IsEvalContaminated(IReadOnlySet<string> evalImageHashes, string? framePath)
    {
        if (evalImageHashes is null || evalImageHashes.Count == 0)
            return false;

        var hash = ComputeFileHash(framePath);
        return hash is not null && evalImageHashes.Contains(hash);
    }
}
