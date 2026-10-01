using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Die Fehler eines einzelnen Importschritts samt Begruendungen.
/// </summary>
/// <param name="Schritt">Sprechender Name des Schritts, z.B. "Fotoverteilung".</param>
/// <param name="Anzahl">Wie viele Fehler dieser Schritt gemeldet hat.</param>
/// <param name="Gruende">
/// Konkrete Begruendungen, soweit der Schritt sie liefert. Darf kuerzer sein als
/// <paramref name="Anzahl"/> — ein Sammelprotokoll kann hunderte gleichartige Fehler
/// erzeugen, und der Bericht soll lesbar bleiben.
/// </param>
public sealed record ImportSchrittFehler(
    string Schritt,
    int Anzahl,
    IReadOnlyList<string> Gruende);

/// <summary>
/// Nach Schritten getrennte Fehlerbilanz eines Importlaufs.
///
/// Anlass (Audit 2026-09-05): Der Ein-Knopf-Import meldete "0 Fehler", obwohl einzelne
/// Teilschritte sehr wohl Fehler hatten — die Fotoverteilung zaehlte gar nicht mit, und
/// die Kopierfehler der name-basierten Protokollverteilung wurden nie gelesen. Wer nur
/// eine Gesamtzahl fuehrt, merkt so etwas nicht. Diese Bilanz nennt jeden Schritt einzeln.
///
/// Reines Datenobjekt: keine Datei-, Netz- oder UI-Abhaengigkeit.
/// </summary>
public sealed record ImportFehlerbilanz(IReadOnlyList<ImportSchrittFehler> Schritte)
{
    public static ImportFehlerbilanz Leer { get; } = new(Array.Empty<ImportSchrittFehler>());

    /// <summary>Summe aller Schrittfehler. Muss mit der Gesamtfehlerzahl uebereinstimmen.</summary>
    public int Gesamt => Schritte.Sum(s => s.Anzahl);

    /// <summary>Lesbare Zeilen fuer Bericht und Abschlussmeldung. Leer, wenn fehlerfrei.</summary>
    public IReadOnlyList<string> Berichtszeilen(int maxGruendeJeSchritt = 5)
    {
        var zeilen = new List<string>();
        foreach (var schritt in Schritte.Where(s => s.Anzahl > 0))
        {
            zeilen.Add($"{schritt.Schritt}: {schritt.Anzahl} Fehler");
            foreach (var grund in schritt.Gruende.Take(maxGruendeJeSchritt))
                zeilen.Add($"    {grund}");
            if (schritt.Gruende.Count > maxGruendeJeSchritt)
                zeilen.Add($"    ... und {schritt.Gruende.Count - maxGruendeJeSchritt} weitere");
        }

        return zeilen;
    }
}

/// <summary>
/// Sammelt die Fehler eines Laufs schrittweise ein. Nicht threadsicher — der Importlauf
/// zaehlt seine Fehler nacheinander.
/// </summary>
public sealed class ImportFehlerbilanzSammler
{
    private readonly List<ImportSchrittFehler> _schritte = new();

    /// <summary>Laufende Gesamtzahl. Dieselbe Zahl, die der Import nach aussen meldet.</summary>
    public int Gesamt { get; private set; }

    /// <summary>
    /// Traegt Fehler eines Schritts ein. <paramref name="anzahl"/> 0 oder kleiner wird
    /// ignoriert, damit fehlerfreie Schritte den Bericht nicht aufblaehen.
    /// </summary>
    public void Melde(string schritt, int anzahl, IEnumerable<string>? gruende = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schritt);
        if (anzahl <= 0)
            return;

        _schritte.Add(new ImportSchrittFehler(
            schritt,
            anzahl,
            gruende?.Where(g => !string.IsNullOrWhiteSpace(g)).ToList() ?? (IReadOnlyList<string>)Array.Empty<string>()));
        Gesamt += anzahl;
    }

    /// <summary>Ein einzelner Fehler mit Begruendung.</summary>
    public void Melde(string schritt, string grund) => Melde(schritt, 1, new[] { grund });

    public ImportFehlerbilanz Bilanz() => new(_schritte.ToList());
}
