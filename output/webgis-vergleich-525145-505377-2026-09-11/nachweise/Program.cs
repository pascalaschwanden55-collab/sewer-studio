using AuswertungPro.Next.Application.UseCases;
using System.Text.Json;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Lookup;
using AuswertungPro.Next.Infrastructure.Projects;
var ordner = "C:/Sewer-Studio_KI_4.5/.tmp/webgis-vergleich-525145";
// Nur lesen; keine Repository.Load/Save-Nebenwirkungen. Simulation ausschliesslich im Arbeitsspeicher.
var p = JsonSerializer.Deserialize<Project>(File.ReadAllText(@"D:\Projekte\Sanierungsabnahme_Zone_5.01_GKS_Bürglen\Projektdateien\projekt.json"),JsonProjectRepository.SerializerOptions)!;
var h = p.Data.Single(h=>h.GetFieldValue(FieldKeys.HoldingName)=="525145-505377");
object Anzeigen() { var b = new ObjektaktenBearbeitung(p,h.Id,"haltung"); return ObjektaktenBestandsfelder.Fuer(b,b.Wurzel).Select(f=>new {f.Id,f.Label,f.Speicherfeld,Wert=b.Lies(b.Wurzel,f)}).ToArray(); }
File.WriteAllText(ordner+"/anzeige-vorher.json",JsonSerializer.Serialize(Anzeigen(),JsonProjectRepository.SerializerOptions));
var quelle = new GeoShopXtfLeser().Lies(@"D:\QGIS_V4.2\GeoShop\2026-09\34UR_Abwasser_DSS_2020_1.xtf",BauteilArt.Haltung,["525145-505377"]);
File.WriteAllText(ordner+"/aktueller-leser.json",JsonSerializer.Serialize(quelle,JsonProjectRepository.SerializerOptions));
var ziel = GeoShopZiel.Fuer(h,p); var plan = GeoShopAbgleichPlanBuilder.Baue([ziel],quelle);
File.WriteAllText(ordner+"/abgleich-vorschau.txt",GeoShopAbgleichBericht.Schreibe(plan));
GeoShopAbgleichAnwender.WendeAn(plan,[ziel]);
File.WriteAllText(ordner+"/anzeige-nach-simulation.json",JsonSerializer.Serialize(Anzeigen(),JsonProjectRepository.SerializerOptions));
File.WriteAllText(ordner+"/akten-nach-simulation.json",JsonSerializer.Serialize(p.Objektakten,JsonProjectRepository.SerializerOptions));
Console.WriteLine("Pruefung und Simulation fertig. Originalprojekt nicht gespeichert.");

