using System;
using System.IO;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Maintenance;

namespace AuswertungPro.Next.UI.Ai.KnowledgeBase;

/// <summary>
/// Phase 6.4 (2026-05-08): Konkrete Implementierung von
/// <see cref="IKnowledgeMirrorOptionsProvider"/> fuer den UI-Layer.
///
/// Aufloese-Reihenfolge fuer <c>MirrorRoot</c> (in dieser Reihenfolge gewinnt der erste Treffer):
///   1. <c>AppSettings.BrainMirrorPath</c> aus <c>%LocalAppData%/SewerStudio/settings.json</c>.
///   2. Environment-Variable <c>SEWERSTUDIO_BRAIN_PATH</c>.
///   3. Hardcoded Default <see cref="KnowledgeMirrorOptions.DefaultMirrorRoot"/>
///      (<c>E:\Brain</c>).
///
/// Wenn das resultierende Laufwerk nicht existiert (z.B. externe Platte ab),
/// wird <c>MirrorRoot=null</c> zurueckgegeben — Caller koennen daran erkennen
/// dass der Mirror deaktiviert ist (statt blind eine OS-Exception zu werfen).
///
/// <c>LastSyncUtc</c> + <c>LastChecksum</c> werden aus der <c>manifest.json</c>
/// im Mirror-Root gelesen (geschrieben von <see cref="KnowledgeMirrorVerifier"/>).
///
/// <c>Excludes</c> liefert aktuell die statische Default-Liste — eine kuenftige
/// Iteration kann diese aus AppSettings persistieren.
///
/// Bewusst NICHT migriert in dieser Iteration (rueckwaerts-kompatibel):
///   - <c>KnowledgeMirrorService</c> liest Excludes weiterhin aus eigenen
///     Konstanten (<c>SkipDirNames</c>, <c>SkipFileExtensions</c>).
///   - <c>KnowledgeBaseModule.StartBrainMirror</c> nutzt weiterhin
///     <c>settings.BrainMirrorPath ?? KnowledgeRoot.ResolveBrainMirrorPath()</c>.
///   - <c>KnowledgeRoot.ResolveBrainMirrorPath</c> bleibt als Direkt-Resolver
///     fuer den Restore-Pfad in <c>KnowledgeRoot.GetRoot</c>.
/// Diese Caller stellen sich graduell auf den Provider um.
/// </summary>
public sealed class KnowledgeMirrorOptionsProvider : IKnowledgeMirrorOptionsProvider
{
    private const string EnvVarName = "SEWERSTUDIO_BRAIN_PATH";

    private readonly Func<AppSettings> _settingsLoader;

    /// <summary>
    /// Default-Konstruktor verwendet <see cref="AppSettings.Load"/>.
    /// </summary>
    public KnowledgeMirrorOptionsProvider() : this(AppSettings.Load) { }

    /// <summary>
    /// Test-Konstruktor mit injizierbarem Settings-Loader.
    /// </summary>
    public KnowledgeMirrorOptionsProvider(Func<AppSettings> settingsLoader)
    {
        _settingsLoader = settingsLoader ?? throw new ArgumentNullException(nameof(settingsLoader));
    }

    /// <inheritdoc />
    public KnowledgeMirrorOptions Load()
    {
        var mirrorRoot = ResolveMirrorRoot();
        var (lastSyncUtc, lastChecksum) = ReadManifestState(mirrorRoot);

        return new KnowledgeMirrorOptions(
            MirrorRoot: mirrorRoot,
            Excludes: KnowledgeMirrorOptions.DefaultExcludes,
            LastSyncUtc: lastSyncUtc,
            LastChecksum: lastChecksum,
            // LastRestoreSource ist heute nicht persistiert — Phase 6.4+:
            // wenn TryRestoreFromBrain den Pfad in settings.json schreibt,
            // hier auslesen. Bis dahin null.
            LastRestoreSource: null);
    }

    /// <summary>
    /// Loest den Mirror-Root nach der Prioritaetskette auf:
    /// AppSettings &gt; Env &gt; Default. Liefert null wenn das ermittelte
    /// Laufwerk nicht existiert (Mirror deaktiviert).
    /// </summary>
    private string? ResolveMirrorRoot()
    {
        string? path = null;

        // 1. AppSettings (User-Override)
        try
        {
            var settings = _settingsLoader();
            if (!string.IsNullOrWhiteSpace(settings.BrainMirrorPath))
                path = settings.BrainMirrorPath!.Trim();
        }
        catch
        {
            // settings.json korrupt oder nicht lesbar — Fallback auf Env/Default.
        }

        // 2. Environment
        if (string.IsNullOrWhiteSpace(path))
        {
            var envBrain = Environment.GetEnvironmentVariable(EnvVarName)?.Trim();
            if (!string.IsNullOrEmpty(envBrain))
                path = envBrain;
        }

        // 3. Default
        if (string.IsNullOrWhiteSpace(path))
            path = KnowledgeMirrorOptions.DefaultMirrorRoot;

        // Laufwerk-Check: wenn das Root-Drive (z.B. E:\) nicht existiert,
        // ist der Mirror nicht verfuegbar — null signalisiert "deaktiviert".
        try
        {
            var drive = Path.GetPathRoot(path);
            if (!string.IsNullOrEmpty(drive) && !Directory.Exists(drive))
                return null;
        }
        catch
        {
            return null;
        }

        return path;
    }

    /// <summary>
    /// Liest <c>LastSyncUtc</c> + <c>LastChecksum</c> aus der <c>manifest.json</c>
    /// im Mirror-Root. Gibt <c>(null, null)</c> zurueck wenn Manifest fehlt
    /// oder Mirror nicht erreichbar ist.
    /// </summary>
    private static (DateTimeOffset? LastSyncUtc, string? LastChecksum) ReadManifestState(string? mirrorRoot)
    {
        if (string.IsNullOrWhiteSpace(mirrorRoot))
            return (null, null);

        try
        {
            if (!Directory.Exists(mirrorRoot))
                return (null, null);

            var manifest = KnowledgeMirrorVerifier.ReadManifest(mirrorRoot);
            if (manifest is null)
                return (null, null);

            // MirrorManifest.WrittenUtc ist DateTime (Kind=Utc per Konvention).
            var written = DateTime.SpecifyKind(manifest.WrittenUtc, DateTimeKind.Utc);
            return (new DateTimeOffset(written), manifest.Sha256);
        }
        catch
        {
            // Best-effort — Manifest-Lesefehler darf den Provider nicht killen.
            return (null, null);
        }
    }
}
