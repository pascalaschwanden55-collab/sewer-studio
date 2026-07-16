using AuswertungPro.Next.Application.Ai.Teacher;

namespace AuswertungPro.Next.Infrastructure.Ai.Teacher;

/// <summary>
/// Kompatibilitätsfassade für die persistierte VSA-zu-YOLO-Klassenkarte.
/// </summary>
public static class VsaYoloClassMap
{
    // Feldname bleibt für bestehende Strukturprüfungen erhalten.
    private static readonly IReadOnlyDictionary<string, int> _defaults =
        VsaYoloClassMapFileStore.Defaults;

    private static readonly IVsaYoloClassMapStore Default = new VsaYoloClassMapFileStore();

    public static IVsaYoloClassMapStore Current => Default;

    [Obsolete("Die VSA-YOLO-Fassade ist unveraenderbar. Abhaengigkeit direkt uebergeben.")]
    public static void Use(IVsaYoloClassMapStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        throw new NotSupportedException(
            "Die VSA-YOLO-Fassade kann nicht mehr global ersetzt werden.");
    }

    public static int GetClassId(string vsaCode)
        => Current.GetClassId(vsaCode);

    public static Dictionary<string, int> GetFullMap()
        => Current.GetFullMap();

    public static Task ExportClassesTxtAsync(string outputPath)
        => Current.ExportClassesTxtAsync(outputPath);
}
