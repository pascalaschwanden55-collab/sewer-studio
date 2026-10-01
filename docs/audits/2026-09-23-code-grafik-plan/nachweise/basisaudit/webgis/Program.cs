using System.Net;
using System.Text;
using System.Text.Json;
using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.WebGis;

static class Program
{
    static readonly List<object> Results = new();
    const string Status = "7ecf9743-e8df-c35d-fdce-1a188b72bef8";
    static async Task Main()
    {
        await HttpRace();
        await MeasureDespiteConflict();
        await DuplicateAfterPreview();
        await MissingVendorMaterial();
        await SessionFailsAfterWrite();
        Console.WriteLine(JsonSerializer.Serialize(Results, new JsonSerializerOptions { WriteIndented = true }));
    }
    static void Result(string name, bool safetyHolds, object evidence) => Results.Add(new { name, safetyHolds, evidence });
    static Project Holding(string? id = "G1")
    {
        var p = new Project(); var h = new HaltungRecord { WebGisGlobalId = id };
        h.SetFieldValue(FieldKeys.HoldingName, "H1", FieldSource.Xtf, false); p.Data.Add(h); return p;
    }
    static WebGisLesestand Stand(string name = "H1")
    {
        var s = new WebGisLesestand { GlobalId = "G1", Bezeichnung = name };
        s.Felder[Status] = "2";
        s.Kataloge[Status] = new() { ("1", "In Betrieb"), ("2", "Ausser Betrieb"), ("5", "Tot/Aufgehoben, verfüllt") };
        return s;
    }
    static async Task HttpRace()
    {
        var handler = new RaceHandler();
        var z = new WebGisZugang { BasisUrl="https://example.test", Projekt="p", Datenquelle="p", JSessionId="TEST", SynSessionId="TEST", SynLogin="audit" };
        var client = new GeonisWebGisClient(new HttpClient(handler), () => z);
        var p = Holding(); p.Data[0].SetFieldValue("Status", "In Betrieb", FieldSource.Manual, true);
        var useCase = new WebGisExportUseCase(client);
        var plan = await useCase.BauePlanAsync(p);
        await useCase.FuehreAusAsync(plan, probelauf:false);
        Result("Fremdaenderung_im_letzten_Client_Lesen", handler.Saves == 0,
            new { handler.Reads, handler.Saves, handler.StatusBeforeSave, handler.StatusAfterSave, plan.Positionen[0].Geschrieben, plan.Positionen[0].SchreibFehler });
    }
    static async Task MeasureDespiteConflict()
    {
        var p = Holding(); p.Data[0].SetFieldValue(FieldKeys.ConditionClass, "4", FieldSource.Xtf, false);
        var a = new ObjektAkte { Art="sanierung", Bezuege = new() { p.Data[0].Id } };
        a.Werte[WebGisSanierungFeldkarte.AkteArt] = new() { Text="Renovierung" };
        a.Werte[WebGisSanierungFeldkarte.AkteStatus] = new() { Text="Ausgeführt" };
        a.Werte[WebGisSanierungFeldkarte.AkteVerfahren] = new() { Text="Schlauchverfahren" }; p.Objektakten.Add(a);
        var stamp = "alt";
        var f = new Fake { Read = (_,_) => { var s=Stand(); s.Felder[WebGisFeldkarte.HaltungZustandRef]="102"; s.Felder["aenderungsdatum"]=stamp; return s; } };
        var u = new WebGisExportUseCase(f); var plan=await u.BauePlanAsync(p); stamp="neu";
        await u.FuehreAusAsync(plan, false);
        Result("Massnahme_trotz_Konfliktsperre_des_Elternobjekts", f.Measures == 0,
            new { f.Writes, f.Measures, ParentError=plan.Positionen[0].SchreibFehler, MeasureWritten=plan.Sanierungen[0].Geschrieben });
    }
    static async Task DuplicateAfterPreview()
    {
        var p=Holding(null); var f=new Fake { Read=(_,_)=>Stand() }; var u=new WebGisImportUseCase(f);
        var plan=await u.BauePlanAsync(p);
        var second=new HaltungRecord { WebGisGlobalId="G1" }; second.SetFieldValue(FieldKeys.HoldingName,"H2",FieldSource.Xtf,false); p.Data.Add(second);
        var result=await u.UebernimmGeprueftAsync(plan,p);
        Result("Dublette_nach_Vorschau_uebernimmt_trotzdem_Werte", string.IsNullOrWhiteSpace(p.Data[0].GetFieldValue("Status")),
            new { result.Uebernommen, result.Gestoppt, FirstId=p.Data[0].WebGisGlobalId, FirstStatus=p.Data[0].GetFieldValue("Status"), SecondId=second.WebGisGlobalId, Sperren=plan.Positionen[0].Sperren });
    }
    static async Task MissingVendorMaterial()
    {
        const string detail="5eeb92cf-a23f-ed9c-9ed2-cd96fdcd7728", group="57efe6f1-4e76-3844-d13b-4c6dc0e1301f";
        var p=new Project(); var r=new SchachtRecord { WebGisGlobalId="G1" }; r.SetFieldValue("Schachtnummer","S1",FieldSource.Xtf,false);
        r.SetFieldValue("Material","Beton, Fertigteil",FieldSource.Xtf,false); p.SchaechteData.Add(r);
        WebGisLesestand Make()
        {
            var s=Stand("S1"); s.Felder[group]="3"; s.Felder[detail]="201";
            s.Kataloge[group]=new() {("1","Beton"),("3","Kunststoff")}; s.Kataloge[detail]=new() {("201","Polyvinylchlorid")}; return s;
        }
        var f=new Fake { Read=(_,_)=>Make(), Catalog=(_,_,_)=>new List<(string,string)>{("104","Beton, Fertigteil")} };
        var u=new WebGisExportUseCase(f); var vendor=await u.BauePlanAsync(p); var vendorCalls=f.CatalogReads;
        r.SetFieldValue("Material","Beton, Fertigteil",FieldSource.Manual,true);
        var manual=await u.BauePlanAsync(p);
        Result("Kanalfirmen_Material_nicht_vorgeschlagen", vendor.Positionen[0].Vorschlaege.Count > 0,
            new { VendorCatalogReads=vendorCalls, VendorSuggestions=vendor.Positionen[0].Vorschlaege.Count, VendorWarnings=vendor.Positionen[0].Hinweise, ManualCatalogReads=f.CatalogReads-vendorCalls, ManualChanges=manual.Positionen[0].Aenderungen.Count });
    }
    static async Task SessionFailsAfterWrite()
    {
        var p=Holding(); p.Data[0].SetFieldValue("Status","In Betrieb",FieldSource.Manual,true);
        var reads=0;
        var f=new Fake { Read=(_,_)=>++reads==3 ? throw new WebGisSitzungException("Nachkontrolle: Sitzung abgelaufen") : Stand() };
        var u=new WebGisExportUseCase(f); var plan=await u.BauePlanAsync(p); var threw=false;
        try { await u.FuehreAusAsync(plan,false); } catch(WebGisSitzungException) { threw=true; }
        Result("Bestaetigtes_Schreiben_bei_Sitzungsfehler_falsch_gemeldet", plan.Positionen[0].Geschrieben || !(plan.Positionen[0].SchreibFehler?.Contains("nicht geschrieben")??false),
            new { ServerConfirmedWrites=f.Writes, SessionException=threw, plan.Positionen[0].Geschrieben, plan.Positionen[0].SchreibFehler });
    }
    sealed class Fake : IGeonisWebGisClient
    {
        public Func<WebGisObjektart,string,WebGisLesestand?> Read=(_,_)=>null;
        public Func<WebGisObjektart,string,string,IReadOnlyList<(string,string)>?> Catalog=(_,_,_)=>null;
        public int Writes, Measures, CatalogReads;
        public Task<WebGisLesestand?> LeseAsync(WebGisObjektart a,string n,CancellationToken ct=default)=>Task.FromResult(Read(a,n));
        public Task<WebGisLesestand?> LeseUeberGlobalIdAsync(WebGisObjektart a,string n,CancellationToken ct=default)=>Task.FromResult(Read(a,n));
        public Task<WebGisSchreibErgebnis> SchreibeAsync(WebGisObjektart a,string n,IReadOnlyDictionary<string,string> f,CancellationToken ct=default) { Writes++; return Task.FromResult(WebGisSchreibErgebnis.Ok()); }
        public Task<WebGisSchreibErgebnis> ErstelleSanierungAsync(WebGisObjektart a,string n,IReadOnlyDictionary<string,string> f,CancellationToken ct=default) { Measures++; return Task.FromResult(WebGisSchreibErgebnis.Ok("M1")); }
        public Task<IReadOnlyList<(string Key,string Text)>?> LeseKatalogListeAsync(WebGisObjektart a,string r,string g,string? t=null,CancellationToken ct=default) { CatalogReads++; return Task.FromResult(Catalog(a,r,g)); }
        public Task<WebGisSanierungKatalog?> LeseSanierungKatalogAsync(WebGisObjektart a,string n,CancellationToken ct=default)
        {
            var k=new WebGisSanierungKatalog(); k.Setze(WebGisSanierungFeldkarte.ArtRef,new[]{("4","Renovierung")});
            k.Setze(WebGisSanierungFeldkarte.StatusRef,new[]{("1","Ausgeführt")}); k.Setze(WebGisSanierungFeldkarte.VerfahrenRef,new[]{("27","Schlauchverfahren")},"4");
            return Task.FromResult<WebGisSanierungKatalog?>(k);
        }
    }
    sealed class RaceHandler : HttpMessageHandler
    {
        public int Reads, Saves; public string? StatusBeforeSave, StatusAfterSave; string current="2";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken ct)
        {
            string json;
            if(req.RequestUri!.AbsolutePath.Contains("saveData"))
            {
                Saves++; StatusBeforeSave=current;
                var body=await req.Content!.ReadAsStringAsync(ct); var raw=body.Split('&').Single(x=>x.StartsWith("jsonobject="))[11..];
                using var doc=JsonDocument.Parse(Uri.UnescapeDataString(raw));
                current=doc.RootElement.GetProperty("components").EnumerateArray().Single(x=>x.GetProperty("refId").GetString()==Status).GetProperty("value").GetString()!;
                StatusAfterSave=current; json="{\"isFailure\":false}";
            }
            else
            {
                Reads++; if(Reads==3) current="5";
                json=JsonSerializer.Serialize(new object[] {new {components=Array.Empty<object>()},new {components=new object[]{
                    new {refId=WebGisFeldkarte.HaltungBezeichnungRef,value="H1"},
                    new {refId=Status,keySelected=current,keys=new[]{"1","2","5"},values=new[]{"In Betrieb","Ausser Betrieb","Tot/Aufgehoben, verfüllt"}}
                }}});
            }
            return new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent(json,Encoding.UTF8,"application/json")};
        }
    }
}
