namespace AuswertungPro.Next.Infrastructure.Map;

/// <summary>
/// Stellt den schnellen Haltungsindex aus der Kataster-XTF oder ihrer TSV-Tabelle bereit.
/// </summary>
public interface IHaltungCadastreIndexProvider
{
    HaltungCadastreIndex EnsureAndLoad(string? xtfPath, string? tablePath = null);

    HaltungCadastreIndex Load(string tablePath);
}

/// <summary>
/// Kapselt Tabellenpruefung, optionalen Neuaufbau und das Laden des Kataster-Index.
/// </summary>
public sealed class HaltungCadastreIndexProvider : IHaltungCadastreIndexProvider
{
    public HaltungCadastreIndex EnsureAndLoad(string? xtfPath, string? tablePath = null)
    {
        var table = string.IsNullOrWhiteSpace(tablePath)
            ? HaltungCadastreIndex.DefaultTablePath
            : tablePath!;

        if (!string.IsNullOrWhiteSpace(xtfPath)
            && File.Exists(xtfPath)
            && !HaltungCadastreExtractor.IsTableFresh(table, xtfPath))
        {
            HaltungCadastreExtractor.BuildTable(xtfPath, table);
        }

        return File.Exists(table)
            ? Load(table)
            : HaltungCadastreIndex.Create(Array.Empty<CadastreHaltung>());
    }

    public HaltungCadastreIndex Load(string tablePath)
        => HaltungCadastreIndex.Create(HaltungCadastreExtractor.ReadTable(tablePath));
}
