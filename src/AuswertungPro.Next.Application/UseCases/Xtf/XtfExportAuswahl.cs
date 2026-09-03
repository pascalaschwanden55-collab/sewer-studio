namespace AuswertungPro.Next.Application.UseCases.Xtf;

/// <summary>Eine beim Import abgelegte Kopie der Original-XTF im Projekt.</summary>
public sealed record XtfProjektkopie(string Pfad, DateTime GeaendertLokal);

/// <summary>Die zwei Wege der Exportseite in der Sprache des Nutzers.</summary>
public enum XtfExportWeg
{
    /// <summary>Bestehende Katasterdaten aktualisieren (technisch: Revision an den Original-TIDs).</summary>
    Aktualisieren,

    /// <summary>Neue eigenstaendige XTF erstellen (technisch: Erstexport mit eigenen Kennungen).</summary>
    Neu
}

/// <summary>
/// Entscheidet aus den Importkopien des Projekts, welcher XTF-Weg empfohlen wird, und
/// formuliert die Zeile ueber das Original sowie den Hinweis am Neuexport. Reine Rechnung:
/// Mit Kopie ist Aktualisieren richtig (sonst entstehen Duplikate im Kataster); ohne Kopie
/// gibt es nichts zu aktualisieren, dann ist der Neuexport der einzige Weg.
/// </summary>
public sealed record XtfExportAuswahl(XtfExportWeg Empfohlen, string OriginalZeile, string NeuHinweis)
{
    public const string OhneKopie = "Keine Importkopie im Projekt — beim Start wählst du die Original-XTF.";
    public const string NeuFuerFehlende = "Für Leitungen, die im Kataster noch fehlen.";
    public const string NeuKannDuplizieren = "Kann Duplikate erzeugen: Das Projekt stammt aus einer Katasterdatei — für Änderungen daran den Weg oben verwenden.";

    public static XtfExportAuswahl Aus(IReadOnlyList<XtfProjektkopie> kopien)
    {
        ArgumentNullException.ThrowIfNull(kopien);

        if (kopien.Count == 0)
            return new XtfExportAuswahl(XtfExportWeg.Neu, OhneKopie, NeuFuerFehlende);

        var neueste = kopien.OrderByDescending(k => k.GeaendertLokal).First();
        var zeile = $"Original: {Path.GetFileName(neueste.Pfad)} — Importkopie vom {neueste.GeaendertLokal:dd.MM.yyyy}";
        if (kopien.Count > 1)
            zeile += $" · + {kopien.Count - 1} weitere";

        return new XtfExportAuswahl(XtfExportWeg.Aktualisieren, zeile, NeuKannDuplizieren);
    }
}
