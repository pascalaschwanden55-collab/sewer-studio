namespace AuswertungPro.Next.Application.Dossiers.Preview;

/// <summary>
/// Eine im erzeugten PDF benannte Stelle: die Word-Textmarke eines fuellbaren
/// Feldes, aufgeloest auf Seite und Position.
///
/// Das ist die exakte Auskunft, die der PDF sonst fehlt. Ohne sie muss die
/// Vorschau ein Feld an seinem Text wiedererkennen und verweigert bewusst jeden
/// Treffer, sobald mehrere Felder denselben Text tragen.
///
/// <see cref="Y"/> ist der PDF-Wert und zaehlt vom unteren Blattrand.
/// </summary>
public sealed record DossierPdfFieldAnchor(
    string MarkerName,
    int PageNumber,
    double X,
    double Y);
