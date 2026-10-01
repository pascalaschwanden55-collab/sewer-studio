using System.Reflection;
using System.Text.Json;
using System.IO;
using System.Linq.Expressions;
using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.UI.DataPage;

var map = XtfStammdatenPlanBuilder.Felder.Concat(XtfStammdatenPlanBuilder.HaltungFelder)
    .Concat(XtfStammdatenPlanBuilder.RohrprofilFelder).ToDictionary(k=>k.Value,k=>k.Key);
var fields = FieldCatalog.ColumnOrder.Append(FieldKeys.SlopePromille).Distinct().Select(f => new {
    Field=f, Label=FieldCatalog.Get(f).Label, Type=FieldCatalog.Get(f).Type.ToString(),
    ExportTarget=map.GetValueOrDefault(f),
    Values=FieldCatalog.GetComboItems(f).Select(v=>new { Display=v, Export=map.TryGetValue(f,out var target)?XtfStammdatenPlanBuilder.NachXtfWert(target,v,"SIA405_ABWASSER_2020_1_LV95"):null})
}).ToArray();
var policy = typeof(GridDropdownFieldPolicy).Assembly.GetType("AuswertungPro.Next.UI.DataPage.SchaechteColumnPolicy")!;
var names = new[]{"Funktion","Material","Status","Sanierungsbedarf","Baujahr","Nutzungsart","Bauwerksart","Versickerungsart","Schachtform","Belastungsklasse","Eigentümer","Bemerkungen","Inspektionsdatum","Primäre Schäden","Fotos","Ausgeführt durch"};
var shaftFields = names.Select(f=>new {Field=f, OptionField=policy.GetMethod("ResolveOptionField")!.Invoke(null,[f]),Group=policy.GetMethod("ResolveSchachtDetailGroup")!.Invoke(null,[f])}).ToArray();
var shaftEnums = new[]{
    new { Field="Funktion", Values=SchachtFunktionVokabular.Auswahl.Select(v=>new{Display=v,Export=XtfSchachtPlanBuilder.NachXtfWert("Funktion",v)}).ToArray()},
    new { Field="Material", Values=SchachtMaterialVokabular.Auswahl.Select(v=>new{Display=v,Export=XtfSchachtPlanBuilder.NachXtfWert("Material",v)}).ToArray()}
};
var allFunctions=SchachtFunktionVokabular.Auswahl.Concat(AbwasserbauwerkVokabular.Spezialfunktionen.Select(SchachtFunktionVokabular.Normalisieren)).Distinct().ToArray();
var emptyRecord = new Project().CreateNewRecord();
var deleteRecord = new HaltungRecord();
deleteRecord.SetFieldValue(FieldKeys.HoldingName,"A-B",FieldSource.Manual,true);
deleteRecord.SetFieldValue(FieldKeys.Remarks,"",FieldSource.Manual,true);
var deletePlan=XtfStammdatenPlanBuilder.Build([deleteRecord],[new XtfStammdatenElement("k1","A-B",new Dictionary<string,string>{{"Bemerkung","Alttext"}},"Kanal")],"SIA405_ABWASSER_2020_1_LV95");
var builderType=typeof(GridDropdownFieldPolicy).Assembly.GetType("AuswertungPro.Next.UI.DataPage.SchaechteRecordDetailsBuilder")!;
var ctor=builderType.GetConstructors(BindingFlags.Instance|BindingFlags.NonPublic).Single();
var commitType=ctor.GetParameters()[2].ParameterType;
var commitParams=commitType.GetMethod("Invoke")!.GetParameters().Select(p=>Expression.Parameter(p.ParameterType)).ToArray();
var commit=Expression.Lambda(commitType,Expression.Empty(),commitParams).Compile();
var builder=ctor.Invoke(new object?[]{new Func<string,IEnumerable<string>>(_=>new[]{"Normwert"}),new Func<string,ICommand?>(_=>null),commit,new Func<bool>(()=>true),null,null});
var actualGroups=(List<AuswertungPro.Next.UI.Views.Windows.RecordDetailGroup>)builderType.GetMethod("Build",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(builder,new object[]{names,new SchachtRecord()})!;
var massSchacht=new SchachtRecord();
massSchacht.SetFieldValue("Schachtnummer","AUDIT-1",FieldSource.Manual,true);
massSchacht.SetFieldValue(FieldKeys.Owner,"Privat",FieldSource.Manual,true);
massSchacht.SetFieldValue(FieldKeys.ShaftDimension1Mm,"4500",FieldSource.Manual,true);
massSchacht.SetFieldValue(FieldKeys.ShaftDimension2Mm,"4500",FieldSource.Manual,true);
massSchacht.SetFieldValue("Funktion","Fettabscheider",FieldSource.Manual,true);
var massPlan=XtfNeuPlanBuilder.Build([], [massSchacht]);
var written=AuswertungPro.Next.Infrastructure.Import.Xtf.XtfNeuWriter.Schreibe(massPlan,
    Path.Combine(AppContext.BaseDirectory,"../../../massprobe-"+Guid.NewGuid().ToString("N")+".xtf"));
var kombi=new SchachtRecord();
kombi.SetFieldValue("Schachtnummer","AUDIT-KOMBI",FieldSource.Manual,true);
kombi.SetFieldValue(FieldKeys.Owner,"Privat",FieldSource.Manual,true);
kombi.SetFieldValue(FieldKeys.ShaftStructureType,"Spezialbauwerk",FieldSource.Manual,true);
kombi.SetFieldValue("Funktion","Kombischacht",FieldSource.Manual,true);
var kombiPlan=XtfNeuPlanBuilder.Build([], [kombi]);
var kombiWritten=AuswertungPro.Next.Infrastructure.Import.Xtf.XtfNeuWriter.Schreibe(kombiPlan,
    Path.Combine(AppContext.BaseDirectory,"../../../kombiprobe-"+Guid.NewGuid().ToString("N")+".xtf"));
var aliasSchacht=new SchachtRecord();
aliasSchacht.SetFieldValue("Schachtnummer","AUDIT-2",FieldSource.Manual,true);
aliasSchacht.SetFieldValue("Baulicher Zustand","2",FieldSource.Manual,true);
var aliasPlan=XtfSchachtPlanBuilder.Build([aliasSchacht],[new XtfStammdatenElement("s1","AUDIT-2",new Dictionary<string,string>{{"BaulicherZustand","Z4"}},"Normschacht")]);
var data = new {Fields=fields,ShaftFields=shaftFields,ShaftEnums=shaftEnums,
  ZusatzFields=XtfZusatzangaben.Felder,
  OtherEnums=new Dictionary<string,IReadOnlyList<string>>{{"Spezialbauwerk.Funktion",AbwasserbauwerkVokabular.Spezialfunktionen},{"Versickerungsanlage.Art",AbwasserbauwerkVokabular.Versickerungsarten}},
  ActualShaftForm=actualGroups.SelectMany(g=>g.Items).Select(i=>new{i.FieldName,i.IsCombo,i.DigitsOnly,i.AllowFreeText}),
  Shaft4500=massPlan, SmallMillimeterInput=new{Input="10 mm",Export=SiaAbmessung.NachMillimeter("10 mm")},
  Written4500=written,
  KombiPlan=kombiPlan,KombiWritten=kombiWritten,
  AliasCondition=new{Resolved=SchachtFeldnamen.Feld(aliasSchacht,FieldKeys.ConditionClass),Positions=aliasPlan.Positionen.Count},
  AllShaftFunctions=allFunctions.Select(v=>new{Display=v,ExportNormschacht=XtfSchachtPlanBuilder.NachXtfWert("Funktion",v),ExportSpezial=AbwasserbauwerkVokabular.Spezialfunktion(v)}),
  ClearRemark=new{Positions=deletePlan.Positionen.Count,Warnings=deletePlan.Hinweise},
  BlankHoldingForm=DataPageRecordDetailsBuilder.Build(emptyRecord,f=>new AuswertungPro.Next.UI.Views.Windows.RecordDetailItem(f,"",_=>{}){FieldName=f}).SelectMany(g=>g.Items).Select(i=>i.FieldName)
};
File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"../../../runtime.json"),JsonSerializer.Serialize(data,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Haltungsfelder: {fields.Length}; Schachtfelder geprüft: {shaftFields.Length}; Leerung Bemerkung: {deletePlan.Positionen.Count} Änderungen, {deletePlan.Hinweise.Count} Hinweise");
