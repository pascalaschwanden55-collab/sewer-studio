namespace AuswertungPro.Next.Infrastructure.Map;

/// <summary>
/// Ein Schacht aus dem amtlichen Abwasserkataster. Die Fachdaten stammen vom
/// Normschacht, die Lage vom gleichnamigen Abwasserknoten.
/// </summary>
public sealed record CadastreSchacht(
    string Bezeichnung,
    string? Funktion,
    string? Material,
    string? Dimension1,
    string? Dimension2,
    string? Status,
    double? Ost,
    double? Nord);

/// <summary>
/// Kompatible statische Fassade. Die XTF- und TSV-Dateizugriffe liegen im
/// injizierbaren <see cref="ISchachtCadastreTableStore"/>.
/// </summary>
public static class SchachtCadastreExtractor
{
    private static readonly ISchachtCadastreTableStore Default =
        new SchachtCadastreTableFileStore();

    public const string TableHeader =
        "Bezeichnung\tFunktion\tMaterial\tDimension1\tDimension2\tStatus\tOst\tNord";

    public static ISchachtCadastreTableStore Current => Default;

    public static IEnumerable<CadastreSchacht> Extract(string xtfPath)
        => Current.Extract(xtfPath);

    public static int BuildTable(string xtfPath, string outTablePath)
        => Current.BuildTable(xtfPath, outTablePath);

    public static IReadOnlyList<CadastreSchacht> ReadTable(string tablePath)
        => Current.ReadTable(tablePath);

    public static bool IsTableFresh(string tablePath, string xtfPath)
        => Current.IsTableFresh(tablePath, xtfPath);
}
