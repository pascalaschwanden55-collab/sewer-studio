// GEONIS-Rueckschrieb: erzeugt aus einem geprueften Projekt eine SIA405-Transferdatei (XTF),
// die den Katasterbestand ueber die OBJ_ID aktualisiert, plus ein Aenderungsprotokoll.
//
// Aufruf:
//   GeonisXtfExport --projekt <projekt.json> --kataster <kataster.xtf> --ziel <ordner>
//                   [--datum yyyy-MM-dd] [--trockenlauf]
//
// Ohne --datum gilt das heutige Datum. Mit --trockenlauf entsteht nur das Protokoll.
// Das Werkzeug veraendert weder das Projekt noch die Katasterdatei; es schreibt ausschliesslich
// in den Zielordner.

using System.Globalization;
using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.Infrastructure.Export.Geonis;
using AuswertungPro.Next.Infrastructure.Projects;

string? projektPfad = null;
string? katasterPfad = null;
string? zielOrdner = null;
var datum = DateOnly.FromDateTime(DateTime.Today);
var trockenlauf = false;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--projekt" or "-p":
            if (i + 1 >= args.Length) return Verwendung();
            projektPfad = args[++i];
            break;
        case "--kataster" or "-k":
            if (i + 1 >= args.Length) return Verwendung();
            katasterPfad = args[++i];
            break;
        case "--ziel" or "-z":
            if (i + 1 >= args.Length) return Verwendung();
            zielOrdner = args[++i];
            break;
        case "--datum" or "-d":
            if (i + 1 >= args.Length) return Verwendung();
            if (!DateOnly.TryParseExact(args[++i], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out datum))
            {
                Console.Error.WriteLine("Fehler: --datum erwartet die Form yyyy-MM-dd, zum Beispiel 2026-09-04.");
                return 2;
            }

            break;
        case "--trockenlauf" or "-t":
            trockenlauf = true;
            break;
        case "--hilfe" or "-h" or "--help":
            return Verwendung();
        default:
            Console.Error.WriteLine($"Fehler: unbekannte Angabe '{args[i]}'.");
            return Verwendung();
    }
}

if (string.IsNullOrWhiteSpace(projektPfad) || string.IsNullOrWhiteSpace(katasterPfad) || string.IsNullOrWhiteSpace(zielOrdner))
{
    Console.Error.WriteLine("Fehler: --projekt, --kataster und --ziel sind Pflicht.");
    return Verwendung();
}

if (!File.Exists(projektPfad))
{
    Console.Error.WriteLine($"Fehler: Projektdatei nicht gefunden: {projektPfad}");
    return 2;
}

if (!File.Exists(katasterPfad))
{
    Console.Error.WriteLine($"Fehler: Kataster-XTF nicht gefunden: {katasterPfad}");
    return 2;
}

var repository = new JsonProjectRepository();
var geladen = repository.Load(projektPfad);
if (!geladen.Ok || geladen.Value is null)
{
    Console.Error.WriteLine($"Fehler beim Laden des Projekts: {geladen.ErrorMessage}");
    return 2;
}

try
{
    var workflow = GeonisXtfExportRuntime.Erzeuge();
    var ergebnis = workflow.Fuehre(new GeonisXtfExportRequest(
        geladen.Value,
        katasterPfad,
        zielOrdner,
        datum,
        trockenlauf));

    Console.WriteLine(ergebnis.Meldung);
    Console.WriteLine($"  Objekte in der Datei : {ergebnis.ObjekteInDatei}");
    Console.WriteLine($"  davon mit Aenderung  : {ergebnis.GeaenderteObjekte}");
    Console.WriteLine($"  Hinweise             : {ergebnis.Hinweise}");
    if (ergebnis.ProtokollPfad is not null)
        Console.WriteLine($"  Protokoll            : {ergebnis.ProtokollPfad}");
    if (ergebnis.XtfPfad is not null)
        Console.WriteLine($"  Transferdatei        : {ergebnis.XtfPfad}");

    Console.WriteLine();
    Console.WriteLine("Vor dem Versand: Protokoll lesen. Nur die dort genannten Attribute sind beurteilt.");

    return ergebnis.Erfolgreich ? 0 : 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Fehler: {ex.Message}");
    return 3;
}

static int Verwendung()
{
    Console.WriteLine("GeonisXtfExport - SIA405-Rueckschrieb fuer GEONIS");
    Console.WriteLine();
    Console.WriteLine("  --projekt  <projekt.json>   geprueftes SewerStudio-Projekt (wird nur gelesen)");
    Console.WriteLine("  --kataster <kataster.xtf>   SIA405-Katasterexport (Quelle der OBJ_ID, wird nur gelesen)");
    Console.WriteLine("  --ziel     <ordner>         Zielordner fuer Transferdatei und Protokoll");
    Console.WriteLine("  --datum    yyyy-MM-dd       Wert fuer Letzte_Aenderung (Standard: heute)");
    Console.WriteLine("  --trockenlauf               nur Protokoll schreiben, keine Transferdatei");
    return 2;
}
