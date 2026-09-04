using System.Globalization;
using System.Text;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Export.Geonis;

namespace AuswertungPro.Next.Infrastructure.Export.Geonis;

/// <summary>
/// Schreibt das Aenderungsprotokoll des Rueckschriebs.
///
/// Das Protokoll ist Pflicht, nicht Beiwerk: In GEONIS gibt es kein Rueckgaengig. Wer den Lauf
/// freigibt, muss vorher schwarz auf weiss sehen, welcher Wert sich wohin bewegt und was
/// bewusst nicht uebernommen wurde.
/// </summary>
public sealed class Sia405ExportProtokollWriter : ISia405ExportProtokollWriter
{
    public void Schreibe(Sia405ExportPlan plan, string zielPfad)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (string.IsNullOrWhiteSpace(zielPfad))
            throw new ArgumentException("Zielpfad fehlt.", nameof(zielPfad));

        var ordner = Path.GetDirectoryName(zielPfad);
        if (!string.IsNullOrEmpty(ordner))
            Directory.CreateDirectory(ordner);

        var text = new StringBuilder();
        SchreibeKopf(plan, text);
        SchreibeAenderungen(plan, text);
        SchreibeMitgeliefert(plan, text);
        SchreibeHinweise(plan, text);

        AtomicTextFileWriter.WriteAllText(zielPfad, text.ToString(), new UTF8Encoding(true));
    }

    private static void SchreibeKopf(Sia405ExportPlan plan, StringBuilder text)
    {
        var modell = plan.Modell.Modelle.Count > 0
            ? string.Join(", ", plan.Modell.Modelle.Select(m => $"{m.Name} {m.Version}".Trim()))
            : "(kein Modelleintrag in der Quelldatei)";

        text.AppendLine("GEONIS-Rueckschrieb - Aenderungsprotokoll");
        text.AppendLine("=========================================");
        text.AppendLine(CultureInfo.InvariantCulture, $"Erstellt            : {DateTime.Now:dd.MM.yyyy HH:mm}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Kataster-Quelle     : {plan.KatasterQuelle}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Aenderungsdatum     : {plan.AenderungsDatum:dd.MM.yyyy}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Modell              : {modell}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Objekte in der Datei: {plan.Objekte.Count}");
        text.AppendLine(CultureInfo.InvariantCulture, $"davon mit Aenderung : {plan.GeaenderteObjekte}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Hinweise            : {plan.Hinweise.Count}");
        text.AppendLine();
        text.AppendLine("Nur die unten aufgefuehrten Attribute sind fachlich beurteilt und duerfen in");
        text.AppendLine("GEONIS uebernommen werden. Alle uebrigen Werte in der Datei stammen unveraendert");
        text.AppendLine("aus dem Katasterexport; sie stehen nur darin, damit die Objekte modellgueltig sind.");
        text.AppendLine("Abgeglichen wird ueber die OBJ_ID, nicht ueber die Bezeichnung.");
        text.AppendLine();
    }

    private static void SchreibeAenderungen(Sia405ExportPlan plan, StringBuilder text)
    {
        text.AppendLine("AENDERUNGEN");
        text.AppendLine("-----------");

        var mitAenderung = plan.Objekte.Where(o => o.Aenderungen.Count > 0).ToList();
        if (mitAenderung.Count == 0)
        {
            text.AppendLine("(keine)");
            text.AppendLine();
            return;
        }

        foreach (var objekt in mitAenderung)
        {
            text.AppendLine(CultureInfo.InvariantCulture,
                $"{objekt.Klasse} {objekt.Bezeichnung}   OBJ_ID {objekt.ObjId}");
            foreach (var aenderung in objekt.Aenderungen)
            {
                var alt = string.IsNullOrWhiteSpace(aenderung.Alt) ? "(leer)" : aenderung.Alt!.Trim();
                text.AppendLine(CultureInfo.InvariantCulture,
                    $"    {aenderung.Attribut,-22}{alt} -> {aenderung.Neu}");
            }

            text.AppendLine();
        }
    }

    private static void SchreibeMitgeliefert(Sia405ExportPlan plan, StringBuilder text)
    {
        var ohneAenderung = plan.Objekte.Where(o => o.Aenderungen.Count == 0).ToList();
        if (ohneAenderung.Count == 0)
            return;

        text.AppendLine("MITGELIEFERT OHNE AENDERUNG");
        text.AppendLine("---------------------------");
        text.AppendLine("Diese Objekte stehen unveraendert in der Datei, damit GEONIS die Breite aus dem");
        text.AppendLine("Hoehen-Breiten-Verhaeltnis ableiten kann. Sie sind nicht zu aktualisieren.");
        foreach (var objekt in ohneAenderung)
        {
            text.AppendLine(CultureInfo.InvariantCulture,
                $"    {objekt.Klasse} {objekt.Bezeichnung}   TID {objekt.Tid}");
        }

        text.AppendLine();
    }

    private static void SchreibeHinweise(Sia405ExportPlan plan, StringBuilder text)
    {
        text.AppendLine("HINWEISE / NICHT UEBERNOMMEN");
        text.AppendLine("----------------------------");
        if (plan.Hinweise.Count == 0)
        {
            text.AppendLine("(keine)");
            return;
        }

        foreach (var hinweis in plan.Hinweise)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"{hinweis.Objekt}: {hinweis.Grund}");
        }
    }
}
