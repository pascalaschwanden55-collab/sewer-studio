using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Media;

internal static class ModernizerStructureFiles
{
    public static bool IsPdf(string path)
        => string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);

    public static string BuildFlatFieldTarget(
        string raw,
        string source,
        string field,
        string holdingRoot,
        string san,
        string stamp,
        int index)
    {
        var ext = Path.GetExtension(source);
        var suffix = "";

        if (string.Equals(field, ModernizerProjectKeys.SecondaryVideoLink, StringComparison.OrdinalIgnoreCase))
            suffix = "-g";
        else if (string.Equals(field, FieldKeys.PdfEigen, StringComparison.OrdinalIgnoreCase))
            suffix = "_E";
        else if (string.Equals(field, FieldKeys.PdfAll, StringComparison.OrdinalIgnoreCase)
                 || string.Equals(field, FieldKeys.PdfPath, StringComparison.OrdinalIgnoreCase))
        {
            if (ModernizerFileNaming.HasDpMarker(raw) || ModernizerFileNaming.HasDpMarker(source))
                suffix = "_DP";
            else if (ModernizerFileNaming.HasEigenMarker(raw) || ModernizerFileNaming.HasEigenMarker(source))
                suffix = "_E";
            else if (index > 0 && string.Equals(field, FieldKeys.PdfAll, StringComparison.OrdinalIgnoreCase))
                suffix = "";
        }

        return Path.Combine(holdingRoot, $"{stamp}_{san}{suffix}{ext}");
    }

    public static string BuildFlatLooseTarget(string source, string holdingRoot, string san, string stamp)
    {
        var ext = Path.GetExtension(source);
        var suffix = "";
        if (IsPdf(source))
        {
            if (ModernizerFileNaming.HasDpMarker(source))
                suffix = "_DP";
            else if (ModernizerFileNaming.HasEigenMarker(source))
                suffix = "_E";
        }
        else if (MediaFileTypes.HasVideoExtension(source)
                 && Path.GetFileNameWithoutExtension(source).Contains("-g", StringComparison.OrdinalIgnoreCase))
        {
            suffix = "-g";
        }

        return Path.Combine(holdingRoot, $"{stamp}_{san}{suffix}{ext}");
    }

    public static string BuildCentralPhotoTarget(string source, string projectFolder, string san)
        => Path.Combine(ProjectStructure.FotosHaltungDir(projectFolder, san), Path.GetFileName(source));
}
