namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>Gemeinsame Masse des Inhaltsverzeichnisses für Word und Vorschau.</summary>
internal static class DossierTocLayoutPolicy
{
    public const string FontFamily = "Arial";
    public const string ColorHex = "000000";
    public const string EntrySpaceBeforeTwips = "120";
    public const string HeadingSpaceAfterTwips = "160";
    public const string EntryFontHalfPoints = "22";
    public const string HeadingFontHalfPoints = "28";
    public const double EntrySpaceBeforePx = 8d;
    public const double HeadingSpaceAfterPx = 10.6666666667d;
    public const double EntryFontPx = 14.6666666667d;
    public const double HeadingFontPx = 18.6666666667d;
}
