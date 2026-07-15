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
    private readonly IHaltungCadastreTableStore _tables;

    public HaltungCadastreIndexProvider()
        : this(HaltungCadastreExtractor.Current)
    {
    }

    public HaltungCadastreIndexProvider(IHaltungCadastreTableStore tables)
    {
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
    }

    public HaltungCadastreIndex EnsureAndLoad(string? xtfPath, string? tablePath = null)
    {
        var table = string.IsNullOrWhiteSpace(tablePath)
            ? HaltungCadastreIndex.DefaultTablePath
            : tablePath!;

        if (!string.IsNullOrWhiteSpace(xtfPath)
            && File.Exists(xtfPath)
            && !_tables.IsTableFresh(table, xtfPath))
        {
            _tables.BuildTable(xtfPath, table);
        }

        return File.Exists(table)
            ? Load(table)
            : HaltungCadastreIndex.Create(Array.Empty<CadastreHaltung>());
    }

    public HaltungCadastreIndex Load(string tablePath)
        => HaltungCadastreIndex.Create(_tables.ReadTable(tablePath));
}
