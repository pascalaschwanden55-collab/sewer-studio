// PdfCodeScanner - liest Kunden-PDFs nur und schreibt einen getrennten JSON-Bericht.

using System.Text;

Console.OutputEncoding = Encoding.UTF8;

if (!PdfCodeScanOptions.TryParse(args, out var options, out var error))
{
    if (!string.IsNullOrWhiteSpace(error))
        Console.Error.WriteLine(error);
    Console.WriteLine("Verwendung: PdfCodeScanner --class BCC [--root D:\\Haltungen] [--out scan.json] [--expect-holdings 439 --expect-findings 1005]");
    return 1;
}

try
{
    var runner = new PdfCodeScanRunner();
    var report = await runner.RunAsync(options!);
    var output = PdfCodeScanWriter.Serialize(report);

    if (options!.OutPath is not null)
    {
        PdfCodeScanWriter.WriteAtomically(options.OutPath, output);
        Console.WriteLine($"Ergebnis: {options.OutPath}");
    }
    else
    {
        Console.WriteLine(output);
    }

    Console.WriteLine($"Haltungen mit {options.CodePrefix}-Befund: {report.Zusammenfassung.HaltungenMitPraefix}");
    Console.WriteLine($"Befunde mit Praefix: {report.Zusammenfassung.BefundeMitPraefix}");
    if (report.Messauswahl is not null)
    {
        Console.WriteLine($"BCC-Haltungen nach Ausschluss: {report.Messauswahl.Haltungen}");
        Console.WriteLine($"BCC-Befunde nach Ausschluss: {report.Messauswahl.Befunde}");
    }

    if (report.Bestandsabgleich is { Passt: false } inventoryCheck)
    {
        Console.Error.WriteLine(
            $"Bestandsabgleich fehlgeschlagen: erwartet {inventoryCheck.ErwarteteHaltungen}/{inventoryCheck.ErwarteteBefunde}, "
            + $"gefunden {inventoryCheck.GefundeneHaltungen}/{inventoryCheck.GefundeneBefunde}.");
        return 3;
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Scan abgebrochen: {ex.Message}");
    return 2;
}
