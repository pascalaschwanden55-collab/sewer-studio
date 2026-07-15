using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Sucht im Ordner einer gewaehlten Schacht-PDF nach der passenden
/// Protokoll- oder Foto-PDF und ergaenzt sie ohne Duplikate.
/// </summary>
public sealed class ShaftPdfSelectionExpansionService : IShaftPdfSelectionExpander
{
    public IReadOnlyList<string> Expand(IReadOnlyList<string> selectedPdfFiles)
    {
        var expanded = new HashSet<string>(selectedPdfFiles, StringComparer.OrdinalIgnoreCase);

        foreach (var pdfPath in selectedPdfFiles)
        {
            var directory = Path.GetDirectoryName(pdfPath);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                continue;

            var name = Path.GetFileNameWithoutExtension(pdfPath);
            var isProtocol = name.Contains("schachtprotokoll", StringComparison.OrdinalIgnoreCase);
            var isPhotos = name.Contains("schachtfotos", StringComparison.OrdinalIgnoreCase);
            if (!isProtocol && !isPhotos)
                continue;

            foreach (var sibling in Directory.EnumerateFiles(directory, "*.pdf", SearchOption.TopDirectoryOnly))
            {
                var siblingName = Path.GetFileNameWithoutExtension(sibling);
                if (isProtocol && siblingName.Contains("schachtfotos", StringComparison.OrdinalIgnoreCase))
                    expanded.Add(sibling);
                else if (isPhotos && siblingName.Contains("schachtprotokoll", StringComparison.OrdinalIgnoreCase))
                    expanded.Add(sibling);
            }
        }

        return expanded.ToList();
    }
}
