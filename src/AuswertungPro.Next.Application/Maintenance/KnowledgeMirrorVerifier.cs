using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace AuswertungPro.Next.Application.Maintenance;

/// <summary>
/// Sprint 1 (2026-05-07): SHA256-Verifikation fuer den Brain-Mirror.
///
/// Hintergrund: Bisher kopiert <c>KnowledgeMirrorService.TryRestoreFromBrain</c>
/// die Mirror-DB blind zurueck wenn die lokale DB fehlt. Wenn die Mirror-DB
/// auf dem Weg zu E:\Brain korrupt geworden ist (Strom weg, Festplatte halb
/// abgehaengt), wuerde eine korrupte DB als "wiederhergestellt" aktiv.
///
/// Loesung: Bei jedem Sync wird der SHA256-Hash der DB in <c>manifest.json</c>
/// im Brain-Root abgelegt. Beim Restore wird der Hash neu berechnet und mit
/// dem Manifest verglichen. Mismatch → KEIN Restore (fail-closed), Log + UI.
/// </summary>
public sealed class KnowledgeMirrorVerifier
{
    private const string ManifestFileName = "manifest.json";

    /// <summary>Berechnet den SHA256-Hash einer Datei (hex-kleinbuchstaben).</summary>
    public static string ComputeSha256(string filePath)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Schreibt das Manifest fuer eine Datei in den angegebenen Mirror-Root.
    /// </summary>
    public static void WriteManifest(string mirrorRoot, string fileName, string sha256, long bytes)
    {
        Directory.CreateDirectory(mirrorRoot);
        var manifest = new MirrorManifest(
            FileName: fileName,
            Sha256: sha256,
            Bytes: bytes,
            WrittenUtc: DateTime.UtcNow);
        var path = Path.Combine(mirrorRoot, ManifestFileName);
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    /// <summary>Liest das Manifest aus dem Mirror-Root, oder null wenn fehlt/korrupt.</summary>
    public static MirrorManifest? ReadManifest(string mirrorRoot)
    {
        var path = Path.Combine(mirrorRoot, ManifestFileName);
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<MirrorManifest>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Verifiziert die Mirror-DB: berechnet SHA256 + vergleicht mit Manifest.
    /// Liefert ein Result mit Status und Erklaerung — der Aufrufer entscheidet
    /// ob ein Restore stattfinden darf.
    /// </summary>
    public static VerificationResult Verify(string mirrorDbPath)
    {
        if (!File.Exists(mirrorDbPath))
            return new VerificationResult(VerificationStatus.MirrorMissing, "Mirror-DB existiert nicht", null, null);

        var mirrorRoot = Path.GetDirectoryName(mirrorDbPath)
            ?? throw new ArgumentException("mirrorDbPath hat keinen gueltigen Verzeichnis-Pfad", nameof(mirrorDbPath));
        var manifest = ReadManifest(mirrorRoot);
        if (manifest is null)
            return new VerificationResult(
                VerificationStatus.NoManifest,
                "Kein manifest.json — Verifikation nicht moeglich (Mirror aus aelterer Version)",
                null,
                null);

        var actualHash = ComputeSha256(mirrorDbPath);
        var actualBytes = new FileInfo(mirrorDbPath).Length;

        if (!string.Equals(actualHash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
            return new VerificationResult(
                VerificationStatus.HashMismatch,
                $"SHA256-Mismatch: erwartet {Snippet(manifest.Sha256)}, gefunden {Snippet(actualHash)}",
                actualHash,
                manifest);

        if (actualBytes != manifest.Bytes)
            return new VerificationResult(
                VerificationStatus.SizeMismatch,
                $"Size-Mismatch: erwartet {manifest.Bytes:N0} B, gefunden {actualBytes:N0} B",
                actualHash,
                manifest);

        return new VerificationResult(VerificationStatus.Ok, "Mirror-DB verifiziert", actualHash, manifest);
    }

    private static string Snippet(string s)
        => string.IsNullOrEmpty(s) ? "(leer)"
            : s.Length <= 16 ? s
            : s[..16] + "...";
}

/// <summary>Persistierter Hash + Metadaten der Mirror-DB.</summary>
public sealed record MirrorManifest(
    string FileName,
    string Sha256,
    long Bytes,
    DateTime WrittenUtc);

/// <summary>Status der Verifikation.</summary>
public enum VerificationStatus
{
    /// <summary>Hash + Size stimmen — Restore ist sicher.</summary>
    Ok,
    /// <summary>Mirror-DB existiert nicht — kein Restore moeglich.</summary>
    MirrorMissing,
    /// <summary>Kein Manifest vorhanden — Verifikation nicht moeglich, Aufrufer entscheidet.</summary>
    NoManifest,
    /// <summary>Hash unterscheidet sich — Mirror moeglicherweise korrupt.</summary>
    HashMismatch,
    /// <summary>Groesse unterscheidet sich — Mirror moeglicherweise abgeschnitten.</summary>
    SizeMismatch,
}

/// <summary>Ergebnis einer Mirror-Verifikation.</summary>
public sealed record VerificationResult(
    VerificationStatus Status,
    string Message,
    string? ActualSha256,
    MirrorManifest? Manifest)
{
    /// <summary>True wenn der Restore sicher durchgefuehrt werden darf.</summary>
    public bool IsRestoreSafe => Status == VerificationStatus.Ok;
}
