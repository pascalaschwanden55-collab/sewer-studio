// Kuenstliches Projekt fuer die Sichtpruefung der Nova-Etappe 1.
// Schreibt ueber das echte JsonProjectRepository; kein Kundenprojekt wird beruehrt.
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Projects;

var ziel = args.Length > 0 ? args[0] : throw new ArgumentException("Zielpfad der projekt.json fehlt.");
Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);

var projekt = new Project { Name = "Nova-Sichtprobe", Description = "Kuenstliches Projekt fuer die Bedienpruefung der Nova-Etappe 1" };
projekt.EnsureMetadataDefaults();

string[] strassen = ["Bahnhofstrasse", "Gotthardstrasse", "Kirchweg", "Seestrasse", "Dorfplatz", "Industriestrasse", "Bergweg"];
string[] material = ["Beton", "PVC", "Steinzeug", "PE", "Beton", "Beton", "Kunststoff"];
string[] nutzung = ["Mischabwasser", "Schmutzabwasser", "Regenabwasser"];
string[] massnahmen = ["", "Inliner", "Reparatur Kurzliner", "", "Erneuerung", "", "Anschluss verpressen"];

for (var i = 0; i < 14; i++)
{
    var r = projekt.CreateNewRecord();
    var oben = 10001 + i;
    var unten = oben + 1;
    void Setze(string feld, string wert) => r.SetFieldValue(feld, wert, FieldSource.Manual, userEdited: false);
    Setze(FieldKeys.HoldingName, $"{oben}-{unten}");
    Setze("NR", (i + 1).ToString());
    Setze(FieldKeys.Street, strassen[i % strassen.Length]);
    Setze(FieldKeys.PipeMaterial, material[i % material.Length]);
    Setze(FieldKeys.NominalDiameterMm, (250 + 50 * (i % 5)).ToString());
    Setze(FieldKeys.HoldingLengthMeters, (28.5 + i * 3.7).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
    Setze(FieldKeys.ConditionClass, (i % 5).ToString());
    Setze(FieldKeys.InspectionYear, "2026");
    Setze(FieldKeys.ConstructionYear, (1968 + i * 3).ToString());
    Setze(FieldKeys.UsageType, nutzung[i % nutzung.Length]);
    Setze(FieldKeys.Owner, "Gemeinde Musterdorf");
    Setze(FieldKeys.Remarks, i % 3 == 0 ? "Sichtprobe: Bemerkung zur Haltung" : "");
    Setze(FieldKeys.RecommendedRehabilitationMeasures, massnahmen[i % massnahmen.Length]);
    Setze(FieldKeys.Cost, i % 2 == 0 ? (1200 * (i + 1)).ToString() : "");
    Setze(FieldKeys.PrimaryDamages, i % 2 == 0 ? $"{2.5 + i:0.0} m BAB Riss laengs; {7.1 + i:0.0} m BCA seitlicher Anschluss" : "");

    if (i % 2 == 0)
    {
        var protokoll = new ProtocolDocument { HaltungId = r.GetFieldValue(FieldKeys.HoldingName) };
        protokoll.Current.Entries.Add(new ProtocolEntry { Code = "BCD", Beschreibung = "Rohranfang", MeterStart = 0 });
        protokoll.Current.Entries.Add(new ProtocolEntry { Code = "BAB", Beschreibung = "Riss laengs", MeterStart = 2.5 + i });
        protokoll.Current.Entries.Add(new ProtocolEntry { Code = "BCA", Beschreibung = "Seitlicher Anschluss", MeterStart = 7.1 + i });
        protokoll.Current.Entries.Add(new ProtocolEntry { Code = "BBA", Beschreibung = "Wurzeln", MeterStart = 12.0 + i, MeterEnd = 14.0 + i, IsStreckenschaden = true });
        protokoll.Current.Entries.Add(new ProtocolEntry { Code = "BCE", Beschreibung = "Rohrende", MeterStart = 28.5 + i * 3.7 });
        r.Protocol = protokoll;
    }
    projekt.AddRecord(r);
}

var ergebnis = new JsonProjectRepository().Save(projekt, ziel);
Console.WriteLine(ergebnis.Ok ? $"OK {projekt.Data.Count} Haltungen -> {ziel}" : $"FEHLER {ergebnis.ErrorMessage}");
return ergebnis.Ok ? 0 : 1;
