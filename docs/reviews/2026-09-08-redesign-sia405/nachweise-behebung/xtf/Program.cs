using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

var root = Path.GetFullPath(".tmp/redesign-xtf-validation/ausgabe-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
var project = new Project { Name = "Synthetische_Abnahme" };
foreach(var (art, funktion) in new[] { ("Normschacht", "Fettabscheider"), ("Normschacht", "Kontrollschacht"),
    ("Spezialbauwerk", "Kombischacht"), ("Versickerungsanlage", "Sickerschacht"), ("Einleitstelle", "") })
{
    var s = new SchachtRecord();
    foreach(var (f,v) in new Dictionary<string,string> {
        ["Schachtnummer"]="TEST"+project.SchaechteData.Count, ["Bauwerksart"]=art, ["Funktion"]=funktion,
        [FieldKeys.Owner]="Privat", ["Material"]="Beton", [FieldKeys.ShaftDimension1Mm]="1200",
        [FieldKeys.ShaftDimension2Mm]="1000", [FieldKeys.ShaftShape]="Oval", [FieldKeys.Remarks]="Synthetischer Test",
        [FieldKeys.OperatingStatus]="in Betrieb", [FieldKeys.ConditionClass]="2"})
        s.SetFieldValue(f,v,FieldSource.Manual,true);
    project.SchaechteData.Add(s);
}
var h = project.CreateNewRecord();
foreach(var(f,v) in new Dictionary<string,string> {
    [FieldKeys.HoldingName]="TEST0-TEST1", ["Schacht_oben"]="TEST0", ["Schacht_unten"]="TEST1",
    [FieldKeys.Owner]="Privat", [FieldKeys.PipeMaterial]="Polyethylen", [FieldKeys.UsageType]="Regenwasser",
    [FieldKeys.NominalDiameterMm]="150", [FieldKeys.ProfileType]="Kreisprofil", [FieldKeys.HoldingLengthMeters]="16.85",
    [FieldKeys.Remarks]="Nur synthetische Daten", [FieldKeys.HierarchicalFunction]="PAA.Sammelkanal"})
    h.SetFieldValue(f,v,FieldSource.Manual,true);
project.Data.Add(h);
foreach(var (mode,delta,extra) in new[]{("standard",false,false),("zusatz",false,true),("aenderungen",true,true)})
{
    var result = new XtfNeuExportService().Erzeuge(new(project,Path.Combine(root,mode),NurAenderungen:delta,MitZusatzangaben:extra));
    if(!result.Ok) throw new Exception(result.Fehler+"\n"+result.Bericht);
    File.WriteAllText(Path.Combine(root,mode,"Bericht.txt"),result.Bericht);
    Console.WriteLine(result.Datei);
}
File.WriteAllText(".tmp/redesign-xtf-validation/letzter-ordner.txt",root);
