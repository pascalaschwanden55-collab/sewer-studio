namespace AuswertungPro.Next.Application.Schatten;

/// <summary>
/// Persistenz der Schattenauswertung als EIGENE Datei im Projektordner
/// (nie projekt.json). Vertrag nach dem costs.json-Muster inkl. Lesefehler-Signal:
/// Bei loadError != null darf der Aufrufer NICHT speichern (sonst wuerde eine nur
/// gesperrte/defekte Datei endgueltig ueberschrieben).
/// </summary>
public interface ISchattenAuswertungStore
{
    SchattenAuswertungStore Load(string? projectPath, out string? loadError);
    bool Save(string? projectPath, SchattenAuswertungStore store, out string error);
}
