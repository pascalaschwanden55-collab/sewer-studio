using System.Reflection;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
var a=Assembly.LoadFrom("C:/Sewer-Studio_KI_4.5/tests/AuswertungPro.Next.Infrastructure.Tests/bin/Release/net10.0/AuswertungPro.Next.Infrastructure.Tests.dll");
var p=(Project)a.GetType("AuswertungPro.Next.Infrastructure.Tests.XtfDssVerbundTests")!.GetMethod("Verbund",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,null)!;
var r=new XtfNeuExportService().Erzeuge(new(p,"C:/Sewer-Studio_KI_4.5/.tmp/dss-abnahme/lieferung"));
Console.WriteLine(r.Bericht); Console.WriteLine(r.Fehler); if(!r.Ok) Environment.Exit(1);

var raw=(string)a.GetType("AuswertungPro.Next.Infrastructure.Tests.XtfDssGeometrieTests")!.GetField("Verlauf",BindingFlags.NonPublic|BindingFlags.Static)!.GetRawConstantValue()!;
p.Objektakten[0].Quellen.Single(q=>q.Klasse=="Haltung").Strukturen["Verlauf"]=raw;
p.Data[0].Geonis!.RichtungGedreht=true;
p.Data[0].Geonis!.VonPunkt="chTEST0000000004";p.Data[0].Geonis!.NachPunkt="chTEST0000000003";
p.Data[0].SetFieldValue(FieldKeys.HoldingName,"B-A",FieldSource.Manual,true);
var reverse=new XtfNeuExportService().Erzeuge(new(p,"C:/Sewer-Studio_KI_4.5/.tmp/dss-abnahme/gegenrichtung",MitZusatzangaben:false));
Console.WriteLine(reverse.Bericht);Console.WriteLine(reverse.Fehler);if(!reverse.Ok) Environment.Exit(2);

// Unabhängige Normprobe: jedes unterstützte skalare Attribut mindestens einmal.
p=(Project)a.GetType("AuswertungPro.Next.Infrastructure.Tests.XtfDssVerbundTests")!.GetMethod("Verbund",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,null)!;
const string owner="chTEST0000000005";
ObjektQuellbeleg Q(string cls,string tid,string name) => new(){System="GeoShop-XTF",Modell="DSS_2020_1_LV95",Klasse=cls,Kennung=tid,
 Werte=new(){["Bezeichnung"]=name,["Letzte_Aenderung"]="20260101"},Referenzen=new(){["DatenherrRef"]=owner,["DatenlieferantRef"]=owner}};
int index=30;
foreach(var cls in new[]{"Spezialbauwerk","Versickerungsanlage","Einleitstelle"}) {
 var tid="chTEST"+(index++).ToString("D10");var node="chTEST"+(index++).ToString("D10");var name="Zusatz"+index;
 var shaft=new SchachtRecord{Geonis=new(){Knoten=node,Bauwerk=tid}};shaft.SetFieldValue("Schachtnummer",name,FieldSource.Kataster,false);p.SchaechteData.Add(shaft);
 var q=Q(cls,tid,name);q.Referenzen["EigentuemerRef"]=owner;var n=Q("Abwasserknoten",node,name);n.Referenzen["AbwasserbauwerkRef"]=tid;
 p.Objektakten.Add(new(){Id=shaft.Id,Art="schacht",Quellen=[q,n]});
}
var prof=Q("Rohrprofil","chTEST0000000040","Profilprobe");p.Objektakten[0].Quellen.Add(prof);p.Objektakten[0].Quellen.Single(q=>q.Klasse=="Haltung").Referenzen["RohrprofilRef"]=prof.Kennung;
var hydr=Q("Hydr_Geometrie","chTEST0000000041","Hydraulikprobe");p.Objektakten[1].Quellen.Add(hydr);p.Objektakten[1].Quellen.Single(q=>q.Klasse=="Abwasserknoten").Referenzen["Hydr_GeometrieRef"]=hydr.Kennung;
int felder=0;
foreach(var q in p.Objektakten.SelectMany(a=>a.Quellen).DistinctBy(q=>q.Kennung)) {
 var defs=AuswertungPro.Next.Application.Xtf.Dss.DssExportSchema.Felder(q.Klasse);if(defs is null)continue;
 foreach(var (key,f) in defs){if(f.Kind=="Structure")continue;felder++;
  if(q.Werte.ContainsKey(key))continue;
  q.Werte[key]=f.Kind switch{"Enum"=>f.Values.Contains("unbekannt")?"unbekannt":f.Values[0],"Number"=>f.Minimum,"Date"=>"20260101",_=>"Probe"};
 }
}
var all=new XtfNeuExportService().Erzeuge(new(p,"C:/Sewer-Studio_KI_4.5/.tmp/dss-abnahme/alle-felder",MitZusatzangaben:false));
Console.WriteLine("SKALARE FELDWERTE "+felder);Console.WriteLine(all.Bericht);Console.WriteLine(all.Fehler);if(!all.Ok)Environment.Exit(3);
