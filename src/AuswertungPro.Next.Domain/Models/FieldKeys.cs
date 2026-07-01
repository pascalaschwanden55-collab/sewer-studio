namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Zentrale Feldnamen, die im Projektformat persistiert werden.
/// Werte nicht umbenennen: sie sind Teil der gespeicherten Projektdateien.
/// </summary>
public static class FieldKeys
{
    public const string HoldingName = "Haltungsname";
    public const string Link = "Link";
    public const string PdfPath = "PDF_Path";
    public const string PdfEigen = "PDF_Eigen";
    public const string PdfAll = "PDF_All";
}
