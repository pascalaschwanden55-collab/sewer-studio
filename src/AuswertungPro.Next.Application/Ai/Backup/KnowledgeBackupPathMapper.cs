using System;
using System.IO;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Application.Ai.Backup;

/// <summary>
/// Mappt ZIP-Eintraege auf lokale Pfade mit Path-Traversal-Schutz.
/// Enthaelt keine IO-Abhaengigkeiten — nur reine String-/Pfad-Logik.
/// </summary>
public static class KnowledgeBackupPathMapper
{
    /// <summary>
    /// Praefix-Konstanten fuer den ZIP-Eintragsraum.
    /// </summary>
    public const string PrefixKnowledge = "knowledge/";
    public const string PrefixRoamingAp  = "roaming_auswertungpro/";
    public const string PrefixRoamingSs  = "roaming_sewerstudio/";
    public const string PrefixLocal      = "local_sewerstudio/";

    /// <summary>
    /// Mappt einen ZIP-Eintragsnamen auf einen absoluten lokalen Pfad.
    /// Gibt null zurueck wenn:
    ///   a) kein bekanntes Praefix erkannt wird,
    ///   b) Path-Traversal (../) erkannt wird.
    /// </summary>
    /// <param name="entryName">Relativer Pfad innerhalb des ZIP-Archivs (Vorwaertsschraegstrich).</param>
    /// <param name="knowledgeRoot">Absoluter Pfad des lokalen Knowledge-Root-Verzeichnisses.</param>
    /// <param name="roamingAp">Absoluter Pfad zum Legacy-AuswertungPro-AppData-Verzeichnis.</param>
    /// <param name="roamingSs">Absoluter Pfad zum SewerStudio-Roaming-AppData-Verzeichnis.</param>
    /// <param name="localSs">Absoluter Pfad zum SewerStudio-Local-AppData-Verzeichnis.</param>
    /// <returns>Aufgeloester, normierter absoluter Pfad oder null.</returns>
    public static string? MapEntryToLocalPath(
        string entryName,
        string knowledgeRoot,
        string roamingAp,
        string roamingSs,
        string localSs)
    {
        string? basePath = null;
        string? relativePart = null;

        if (entryName.StartsWith(PrefixKnowledge, StringComparison.Ordinal))
        {
            basePath = knowledgeRoot;
            relativePart = entryName[PrefixKnowledge.Length..];
        }
        else if (entryName.StartsWith(PrefixRoamingAp, StringComparison.Ordinal))
        {
            basePath = roamingAp;
            relativePart = entryName[PrefixRoamingAp.Length..];
        }
        else if (entryName.StartsWith(PrefixRoamingSs, StringComparison.Ordinal))
        {
            basePath = roamingSs;
            relativePart = entryName[PrefixRoamingSs.Length..];
        }
        else if (entryName.StartsWith(PrefixLocal, StringComparison.Ordinal))
        {
            basePath = localSs;
            relativePart = entryName[PrefixLocal.Length..];
        }

        if (basePath is null || relativePart is null)
            return null;

        // Path-Traversal-Schutz: Aufgeloester Pfad muss innerhalb von basePath bleiben
        var combined = Path.Combine(basePath, relativePart.Replace('/', Path.DirectorySeparatorChar));
        var fullBase = Path.GetFullPath(basePath);
        var fullResolved = Path.GetFullPath(combined);

        if (!fullResolved.StartsWith(fullBase + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(fullResolved, fullBase, StringComparison.OrdinalIgnoreCase))
        {
            BestEffort.ReportWarning(
                $"[KnowledgeBackupPathMapper] Path-Traversal blockiert: {entryName} → {fullResolved}");
            return null;
        }

        return fullResolved;
    }
}
