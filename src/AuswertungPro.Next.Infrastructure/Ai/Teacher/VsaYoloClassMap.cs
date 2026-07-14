using System.Threading;
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

    private static IVsaYoloClassMapStore _current = new VsaYoloClassMapFileStore();

    public static IVsaYoloClassMapStore Current => Volatile.Read(ref _current);

    public static void Use(IVsaYoloClassMapStore store)
        => Volatile.Write(
            ref _current,
            store ?? throw new ArgumentNullException(nameof(store)));

    public static int GetClassId(string vsaCode)
        => Current.GetClassId(vsaCode);

    public static Dictionary<string, int> GetFullMap()
        => Current.GetFullMap();

    public static Task ExportClassesTxtAsync(string outputPath)
        => Current.ExportClassesTxtAsync(outputPath);
}
