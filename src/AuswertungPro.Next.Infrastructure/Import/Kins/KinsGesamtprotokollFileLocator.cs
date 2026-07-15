using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import.Kins;

/// <summary>
/// Findet das KINS-Kanalfernseh-Gesamtprotokoll in der Quelle
/// (z.B. 048473_PDF\048473_Protokoll.pdf): groesste PDF mit "Protokoll" im
/// Namen (Deckblatt ausgeschlossen). Es ist die Basis fuer den Seiten-Split
/// je Haltung — die automatische Wahl "groesste PDF im Archiv" wuerde bei
/// KINS sonst Plaene/Dichtheitsprotokolle treffen.
/// </summary>
public sealed class KinsGesamtprotokollFileLocator : IKinsGesamtprotokollLocator
{
    public string? Finde(string sourceFolder)
    {
        try
        {
            return Infrastructure.Common.SafeFileEnumeration.EnumerateFilesSafe(sourceFolder, "*.pdf", recursive: true)
                .Where(p =>
                {
                    var name = Path.GetFileNameWithoutExtension(p);
                    return name.Contains("Protokoll", StringComparison.OrdinalIgnoreCase)
                        && !name.Contains("Deckblatt", StringComparison.OrdinalIgnoreCase);
                })
                .OrderByDescending(p => { try { return new FileInfo(p).Length; } catch { return 0L; } })
                .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}
