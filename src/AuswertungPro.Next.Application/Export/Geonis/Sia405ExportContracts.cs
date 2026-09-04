using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Export.Geonis;

/// <summary>Liest die Kataster-XTF einmal durch und baut den Identitaets- und Ist-Wert-Index.</summary>
public interface ISia405KatasterIndexReader
{
    Sia405KatasterIndex Lies(string katasterXtfPfad);
}

/// <summary>
/// Holt den unveraenderten XML-Quelltext einzelner Objekte (nach TID) aus der Kataster-XTF.
/// Zweiter Lesedurchgang: erst wenn feststeht, welche wenigen Objekte gebraucht werden.
/// </summary>
public interface ISia405ObjektQuelltextLeser
{
    IReadOnlyDictionary<string, string> Lies(string katasterXtfPfad, IReadOnlyCollection<string> tids);
}

/// <summary>Erzeugt den Plan aus Projektdaten und Katasterindex. Reine Regeln, kein Dateizugriff.</summary>
public interface ISia405ExportPlanBuilder
{
    Sia405ExportPlan Erstelle(Project projekt, Sia405KatasterIndex kataster, Sia405ExportOptionen optionen);
}

/// <summary>Schreibt die SIA405-Transferdatei aus Plan und Original-Quelltexten.</summary>
public interface ISia405XtfWriter
{
    void Schreibe(Sia405ExportPlan plan, IReadOnlyDictionary<string, string> quelltexteNachTid, string zielPfad);
}

/// <summary>Schreibt das menschenlesbare Aenderungsprotokoll.</summary>
public interface ISia405ExportProtokollWriter
{
    void Schreibe(Sia405ExportPlan plan, string zielPfad);
}

/// <summary>Einstellungen eines Exportlaufs.</summary>
/// <param name="AenderungsDatum">Wert fuer Letzte_Aenderung der geaenderten Objekte.</param>
/// <param name="KatasterQuelle">Pfad der Kataster-XTF, nur fuer das Protokoll.</param>
public sealed record Sia405ExportOptionen(DateOnly AenderungsDatum, string KatasterQuelle);
