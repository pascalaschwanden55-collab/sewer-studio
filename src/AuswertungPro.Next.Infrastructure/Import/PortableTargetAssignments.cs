using System;
using System.Collections.Generic;
using System.IO;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Haelt fest, welche Projektdatei beim Portabelmachen bereits einem Quellverweis
/// zugeordnet wurde — je Haltung.
///
/// Anlass (Auditbefund 04, 18.09.2026): Zwei verschiedene externe PDF-Verweise wurden
/// beide auf dieselbe lokale Datei umgebogen, weil die Kandidatenwahl bei fehlendem
/// Namenstreffer auf die Datei mit dem kuerzesten Namen zurueckfaellt. Die Dokumente
/// existierten weiter, waren aber falsch zugeordnet.
///
/// Derselbe Quellverweis darf dieselbe Zieldatei mehrfach beanspruchen — `PDF_Path`
/// und ein Eintrag in `PDF_All` nennen oft dasselbe Hauptprotokoll. Ein ANDERER
/// Quellverweis darf es nicht.
/// </summary>
internal sealed class PortableTargetAssignments
{
    private readonly Dictionary<string, string> _vergeben =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>true = Ziel ist frei oder gehoert bereits genau diesem Quellverweis.</summary>
    public bool TryClaim(string zielPfad, string quelle)
    {
        if (string.IsNullOrWhiteSpace(zielPfad))
            return true;

        string schluessel;
        try
        {
            schluessel = Path.GetFullPath(zielPfad);
        }
        catch
        {
            schluessel = zielPfad;
        }

        var quellSchluessel = (quelle ?? string.Empty).Trim();
        if (_vergeben.TryGetValue(schluessel, out var bisher))
            return string.Equals(bisher, quellSchluessel, StringComparison.OrdinalIgnoreCase);

        _vergeben[schluessel] = quellSchluessel;
        return true;
    }
}
