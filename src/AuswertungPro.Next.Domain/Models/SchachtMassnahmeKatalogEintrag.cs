namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Ein Eintrag der selbst gepflegten Schacht-Massnahmen-Liste: Name + manueller Preis
/// (Einheit nur zur Anzeige). Bewusst NPK-frei — kein Katalog, kein NpkCode.
/// </summary>
public sealed record SchachtMassnahmeKatalogEintrag
{
    public string Name { get; init; } = "";
    public decimal Preis { get; init; }
    public string Einheit { get; init; } = "Stk";
}
