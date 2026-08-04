using System.Globalization;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Application.Ai.Training;

public sealed record PdfGoldProvenance(
    string SourceDocumentName,
    string SourceDocumentSha256,
    int PageNumber,
    string? PhotoId,
    string MatchKind);

/// <summary>
/// Prueft die vom PDF-Pruefimport erzeugte, unveraenderliche Herkunftsspur.
/// Freitext oder unvollstaendige Nachweise duerfen ein PDF-Foto nicht zu Gold machen.
/// </summary>
public static partial class PdfGoldProvenancePolicy
{
    private const string MissingPhotoId = "-";

    public static bool IsValid(string? notes)
        => TryParse(notes, out _);

    public static bool TryParse(string? notes, out PdfGoldProvenance provenance)
    {
        provenance = null!;
        if (string.IsNullOrWhiteSpace(notes))
            return false;

        var match = ProvenanceRegex().Match(notes);
        if (!match.Success)
            return false;

        var documentName = match.Groups["document"].Value;
        if (!IsValidDocumentName(documentName))
            return false;

        var sha256 = match.Groups["sha256"].Value;
        if (sha256.Length != 64 || sha256.Any(character => !Uri.IsHexDigit(character)))
            return false;

        if (!int.TryParse(
                match.Groups["page"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var pageNumber)
            || pageNumber <= 0)
        {
            return false;
        }

        var photoId = match.Groups["photo"].Value;
        if (!IsSafeFieldValue(photoId))
            return false;

        var matchKind = match.Groups["matchKind"].Value;
        var isAllowed = matchKind switch
        {
            "same_block" => true,
            "photo_id" or "time_meter_text" => photoId != MissingPhotoId,
            _ => false
        };
        if (!isAllowed)
            return false;

        provenance = new PdfGoldProvenance(
            documentName,
            sha256,
            pageNumber,
            photoId == MissingPhotoId ? null : photoId,
            matchKind);
        return true;
    }

    private static bool IsValidDocumentName(string value)
        => IsSafeFieldValue(value)
           && value.Length > ".pdf".Length
           && value.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
           && value.IndexOfAny(['/', '\\']) < 0;

    private static bool IsSafeFieldValue(string value)
        => value.Length > 0
           && string.Equals(value, value.Trim(), StringComparison.Ordinal)
           && !value.Any(char.IsControl);

    [GeneratedRegex(
        @"\APDF-Operateurreferenz: (?<document>[^;\r\n]+); SHA-256=(?<sha256>[0-9A-Fa-f]{64}); Seite=(?<page>[1-9][0-9]*); Foto=(?<photo>[^;\r\n]+); Zuordnung=(?<matchKind>[a-z_]+)\z",
        RegexOptions.CultureInvariant)]
    private static partial Regex ProvenanceRegex();
}
