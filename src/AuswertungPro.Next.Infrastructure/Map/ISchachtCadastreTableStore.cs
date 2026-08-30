namespace AuswertungPro.Next.Infrastructure.Map;

/// <summary>
/// Liest Schaechte aus der Kataster-XTF und haelt sie als schlanke Tabelle.
/// Die XTF ist mehrere hundert Megabyte gross und wird deshalb nur einmal
/// gelesen; danach beantwortet die Tabelle jede Abfrage.
/// </summary>
public interface ISchachtCadastreTableStore
{
    /// <summary>Liest alle Schaechte direkt aus der XTF.</summary>
    IEnumerable<CadastreSchacht> Extract(string xtfPath);

    /// <summary>Schreibt die Tabelle und liefert die Anzahl der Schaechte.</summary>
    int BuildTable(string xtfPath, string outTablePath);

    /// <summary>Liest eine zuvor geschriebene Tabelle.</summary>
    IReadOnlyList<CadastreSchacht> ReadTable(string tablePath);

    /// <summary>True, wenn die Tabelle noch zur XTF passt.</summary>
    bool IsTableFresh(string tablePath, string xtfPath);
}
