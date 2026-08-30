namespace AuswertungPro.Next.Infrastructure.Map;

/// <summary>
/// Ein Schacht aus dem amtlichen Abwasserkataster. Die Fachdaten stammen vom
/// Normschacht, die Lage vom gleichnamigen Abwasserknoten.
///
/// Bewusst ohne statische Zugriffsfassade: Wer die Tabelle braucht, bekommt
/// <see cref="ISchachtCadastreTableStore"/> in den Konstruktor.
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
