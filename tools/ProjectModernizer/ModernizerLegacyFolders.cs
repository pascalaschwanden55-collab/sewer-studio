internal static class ModernizerLegacyFolders
{
    public const string Haltungen = "Haltungen";
    public const string Imports = "Imports";
    public const string HoldingFotos = "Fotos";
    public const string HoldingPdf = "PDF";
    public const string HoldingVideo = "Video";

    public static IReadOnlyList<string> SchachtFolders { get; } = new[]
    {
        "Sch\u00e4chte_1.15",
        "Schaechte_1.15"
    };

    public static IReadOnlyList<string> HoldingSubfolders { get; } = new[]
    {
        HoldingVideo,
        HoldingPdf,
        HoldingFotos
    };

    public static bool IsDataTreeRoot(string value)
        => string.Equals(value, Haltungen, StringComparison.OrdinalIgnoreCase)
           || SchachtFolders.Any(name => string.Equals(value, name, StringComparison.OrdinalIgnoreCase));
}
