namespace AuswertungPro.Next.Infrastructure.Map;

/// <summary>
/// Eine Haltung aus dem amtlichen Abwasserkataster, reduziert auf den Verteil-Abgleich.
/// </summary>
public sealed record CadastreHaltung(
    string Bezeichnung,
    string ShaftA,
    string ShaftB,
    string? Laenge,
    string? LichteHoehe,
    string? Material);

/// <summary>
/// Kompatible statische Fassade. XTF- und TSV-Dateizugriffe liegen im injizierbaren
/// <see cref="IHaltungCadastreTableStore"/>.
/// </summary>
public static class HaltungCadastreExtractor
{
    private static readonly IHaltungCadastreTableStore Default =
        new HaltungCadastreTableFileStore();

    public const string TableHeader = "Bezeichnung\tShaftA\tShaftB\tLaenge\tLichteHoehe\tMaterial";

    public static IHaltungCadastreTableStore Current => Default;

    [Obsolete("Globaler Austausch wurde entfernt. Den Dienst per Konstruktor uebergeben.")]
    public static void Use(IHaltungCadastreTableStore store) =>
        throw new NotSupportedException(
            "Die globale Kataster-Tabellenablage kann nicht mehr ausgetauscht werden. " +
            "IHaltungCadastreTableStore bitte per Konstruktor uebergeben.");

    public static IEnumerable<CadastreHaltung> Extract(string xtfPath)
        => Current.Extract(xtfPath);

    public static int BuildTable(string xtfPath, string outTablePath)
        => Current.BuildTable(xtfPath, outTablePath);

    public static IReadOnlyList<CadastreHaltung> ReadTable(string tablePath)
        => Current.ReadTable(tablePath);

    public static bool IsTableFresh(string tablePath, string xtfPath)
        => Current.IsTableFresh(tablePath, xtfPath);

    /// <summary>Zerlegt eine Haltungsbezeichnung in die zwei Schachtnummern.</summary>
    public static (string A, string B) SplitShaftPair(string bezeichnung)
    {
        var parts = bezeichnung.Trim().Split(
            '-',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2 ? (parts[0], parts[1]) : (string.Empty, string.Empty);
    }
}
