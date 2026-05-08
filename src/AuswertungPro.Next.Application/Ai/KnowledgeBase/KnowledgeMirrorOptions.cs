using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Ai.KnowledgeBase;

/// <summary>
/// Phase 6.4 (2026-05-08): Single-Source-of-Truth fuer die Brain-Mirror-Konfiguration.
///
/// Bisher war die Konfig auf vier Stellen verteilt:
///   1. <c>AppSettings.BrainMirrorPath</c> (User-Override aus settings.json).
///   2. <c>SEWERSTUDIO_BRAIN_PATH</c> (Environment-Variable).
///   3. Default <c>E:\Brain</c> (hardcoded in <c>KnowledgeRoot.ResolveBrainMirrorPath</c>).
///   4. Skip-Listen <c>SkipDirNames</c> / <c>SkipFileExtensions</c> (hardcoded in
///      <c>KnowledgeMirrorService</c>) plus <c>WrittenUtc</c>/<c>Sha256</c> aus
///      <c>MirrorManifest</c> (Application/Maintenance).
///
/// Dieser Record buendelt alles in einer einzigen, unveraenderlichen Struktur.
/// Caller bleiben unveraendert (rueckwaerts-kompatibel) — graduelle Migration:
/// Aufrufer wechseln auf <see cref="IKnowledgeMirrorOptionsProvider"/>, sobald sie
/// angefasst werden.
/// </summary>
/// <param name="MirrorRoot">
/// Ziel-Pfad des Brain-Mirrors (z.B. <c>"E:\Brain"</c>).
/// <c>null</c> bedeutet: Mirror ist deaktiviert (Laufwerk fehlt oder explizit aus).
/// </param>
/// <param name="Excludes">
/// Liste von Verzeichnis-Namen oder Dateimustern die nicht gespiegelt werden
/// (z.B. <c>"frames/"</c>, <c>"*.tmp"</c>, <c>".git/"</c>). Konvention:
/// Eintrag mit <c>/</c>-Suffix = Verzeichnis-Name, sonst Datei-Glob/Extension.
/// </param>
/// <param name="LastSyncUtc">
/// Zeitpunkt des letzten erfolgreichen Sync (aus <c>manifest.json</c> der Mirror-DB).
/// <c>null</c> wenn noch nie synchronisiert.
/// </param>
/// <param name="LastChecksum">
/// SHA256-Hash der letzten gespiegelten <c>KnowledgeBase.db</c> (hex,
/// klein-geschrieben). <c>null</c> wenn kein Manifest oder Mirror leer.
/// </param>
/// <param name="LastRestoreSource">
/// Pfad aus dem zuletzt restored wurde (z.B. <c>"E:\Brain"</c>). <c>null</c>
/// wenn nie restored — diagnostisches Feld fuer das Diagnostics-Tab.
/// </param>
public sealed record KnowledgeMirrorOptions(
    string? MirrorRoot,
    IReadOnlyList<string> Excludes,
    DateTimeOffset? LastSyncUtc,
    string? LastChecksum,
    string? LastRestoreSource)
{
    /// <summary>
    /// Default-Excludes die dem Verhalten von
    /// <c>KnowledgeMirrorService.SkipDirNames</c> + <c>SkipFileExtensions</c>
    /// entsprechen. Verzeichnisse haben <c>/</c>-Suffix, Datei-Globs beginnen mit
    /// <c>*</c>. Synchron halten mit <c>KnowledgeMirrorService</c> bis die
    /// Migration der Caller abgeschlossen ist.
    /// </summary>
    public static IReadOnlyList<string> DefaultExcludes { get; } = new[]
    {
        // Verzeichnisse (regenerierbar / nicht relevant)
        "frames/",
        "frames_extracted/",
        "frames_temp/",
        "tmp/",
        "temp/",
        "_archive/",
        ".git/",
        "obj/", "bin/",
        "yolo_runs/",
        "florence2_shadow_log/",
        // Datei-Globs
        "*.tmp", "*.temp", "*.log", "*.trace",
        "*.pyc",
        "*.lock", "*.lscache",
    };

    /// <summary>
    /// Hardcoded-Default-Pfad. Wird verwendet wenn weder AppSettings noch
    /// Env-Variable einen Wert liefern. Synchron halten mit
    /// <c>KnowledgeRoot.ResolveBrainMirrorPath()</c>.
    /// </summary>
    public const string DefaultMirrorRoot = @"E:\Brain";

    /// <summary>
    /// Leere Default-Instanz mit allen Defaults. Praktisch fuer Tests die einen
    /// Provider mocken.
    /// </summary>
    public static KnowledgeMirrorOptions Empty { get; } = new(
        MirrorRoot: null,
        Excludes: DefaultExcludes,
        LastSyncUtc: null,
        LastChecksum: null,
        LastRestoreSource: null);
}

/// <summary>
/// Liefert die konsolidierte Brain-Mirror-Konfiguration.
/// Implementierungen lesen aus AppSettings, Environment + Manifest und
/// kombinieren das Ergebnis in einer einzigen <see cref="KnowledgeMirrorOptions"/>.
/// </summary>
public interface IKnowledgeMirrorOptionsProvider
{
    /// <summary>
    /// Liest die aktuelle Konfiguration. Best-effort: bei IO-Fehlern liefert
    /// die Implementierung einen sinnvollen Fallback (z.B. <see cref="KnowledgeMirrorOptions.Empty"/>).
    /// </summary>
    KnowledgeMirrorOptions Load();
}
