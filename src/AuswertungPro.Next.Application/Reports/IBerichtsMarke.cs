namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Gemeinsame Quelle fuer das Logo, das in Berichten (PDF-/Excel-Export, Dossier)
/// verwendet wird. Reine Daten: kein Dateizugriff und keine Fachlogik beim Aufrufer,
/// nur ein bereits geprueftes Ergebnis.
///
/// Die Umsetzung liegt in der UI-Schicht auf den Programmeinstellungen
/// (<c>AppSettingsBerichtsMarke</c>), damit eine geaenderte Einstellung ohne
/// Programmneustart beim naechsten erzeugten Bericht wirkt.
/// </summary>
public interface IBerichtsMarke
{
    /// <summary>
    /// Absoluter Pfad zur Logo-Bilddatei, oder <c>null</c> wenn kein Logo verfuegbar
    /// ist — weder ein eingestelltes noch das mitgelieferte Standardlogo. Ein fehlendes
    /// Logo stoppt keinen Export; der Bericht entsteht dann eben ohne Logo.
    /// </summary>
    string? LogoPfad { get; }
}
