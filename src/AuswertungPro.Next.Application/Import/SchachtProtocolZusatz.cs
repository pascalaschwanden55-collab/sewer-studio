using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Was ein Schachtprotokoll ueber die bisherigen Stammdaten hinaus hergibt: die
/// Anschlusstabelle, Medium, Materialien, den Deckeldurchmesser sowie die Kaestchen
/// «Leiter/Steigeisen» und «Tauchbogen». Alles optional; <c>null</c> heisst «im Protokoll
/// nicht genannt» und wird beim Anwenden nicht geschrieben.
/// </summary>
public sealed record SchachtProtocolZusatz(
    IReadOnlyList<SchachtAnschluss> Anschluesse,
    string? Medium,
    string? MaterialSchacht,
    string? MaterialDeckel,
    string? DeckelDurchmesserMm,
    string? Steighilfe,
    string? Tauchbogen)
{
    public static SchachtProtocolZusatz Leer { get; } = new([], null, null, null, null, null, null);

    public bool IstLeer =>
        Anschluesse.Count == 0
        && Medium is null && MaterialSchacht is null && MaterialDeckel is null
        && DeckelDurchmesserMm is null && Steighilfe is null && Tauchbogen is null;
}
