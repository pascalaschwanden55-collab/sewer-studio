namespace AuswertungPro.Next.UI.Behaviors;

/// <summary>
/// Wird von einer Seite implementiert, deren Filterzustand Teil einer gespeicherten Ansicht sein soll
/// (Dim4). Bewusst schmal und opt-in — nur der Filter ist ohne reines XAML nicht erreichbar; Spalten
/// und Sortierung werden generisch ueber das Grid erfasst.
/// </summary>
public interface ISavedViewFilterProvider
{
    /// <summary>Serialisiert den aktuellen Filterzustand (z.B. als JSON). Null = kein Filter.</summary>
    string? CaptureFilterState();

    /// <summary>Stellt einen zuvor erfassten Filterzustand wieder her.</summary>
    void ApplyFilterState(string? state);
}
