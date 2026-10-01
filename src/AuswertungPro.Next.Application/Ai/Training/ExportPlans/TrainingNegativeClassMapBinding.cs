using AuswertungPro.Next.Application.Ai.Training.ClassMaps;

namespace AuswertungPro.Next.Application.Ai.Training.ExportPlans;

/// <summary>
/// Ein streng gebundenes Negativbild darf nur mit genau der Detect-Klassenkarte v3 ins
/// Training, fuer die es menschlich geprueft wurde: gleiche Version, gleicher Karten- und
/// VSA-Hash, 15 lueckenlose Klassen-IDs, BCC_bogen an Position 14. Die Regel wird bewusst
/// an zwei Grenzen aufgerufen (Planer-Eingabe und Planer), damit keine allein genuegt.
/// </summary>
public static class TrainingNegativeClassMapBinding
{
    public static void Validate(TrainingExportNegativeImage negative, TrainingYoloClassMapSnapshot classMap)
    {
        ArgumentNullException.ThrowIfNull(negative);
        ArgumentNullException.ThrowIfNull(classMap);

        var activeClassIds = classMap.Classes
            .OrderBy(item => item.Value)
            .Select(item => item.Value)
            .ToArray();
        if (negative.ClassMapVersion != 3
            || classMap.Version != 3
            || classMap.ClassMapSha256 is null
            || !string.Equals(negative.ClassMapSha256, classMap.ClassMapSha256, StringComparison.Ordinal)
            || !string.Equals(negative.VsaManifestHash, classMap.VsaManifestHash, StringComparison.Ordinal)
            || activeClassIds.Length != 15
            || !activeClassIds.SequenceEqual(Enumerable.Range(0, 15))
            || !string.Equals(classMap.OrderedClassNames[14], "BCC_bogen", StringComparison.Ordinal))
        {
            throw new TrainingExportPlanException(
                "Das strikte Negativ-Set passt nicht zur aktuell aktiven Detect-Klassenkarte v3.");
        }
    }
}
