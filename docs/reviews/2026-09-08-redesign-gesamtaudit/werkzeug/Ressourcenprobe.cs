using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using AuswertungPro.Next.UI;

internal static partial class Program
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void PruefeEchteRessourcen()
    {
        // Kein Run, Show oder Dispatcher: Der produktive OnStartup bleibt in der Warteschlange.
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.InitializeComponent();
        var result = new Dictionary<string, object>();
        var gruppenMethode = typeof(App).Assembly.GetType("AuswertungPro.Next.UI.DataPage.SchaechteColumnPolicy")!
            .GetMethod("ResolveSchachtDetailGroup", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
        var feldSoll = new[] {
            (Feld: "Baujahr", Soll: "Stammdaten"),
            (Feld: "Belastungsklasse", Soll: "Zustand und Inspektion"),
            (Feld: "Inspektionsdatum", Soll: "Zustand und Inspektion"),
            (Feld: "Primäre Schäden", Soll: "Zustand und Inspektion"),
            (Feld: "Fotos", Soll: "Dokumente und Medien"),
            (Feld: "Bemerkungen", Soll: "Sanierung und Kosten"),
            (Feld: "Ausgefuehrt_durch", Soll: "Sanierung und Kosten"),
            (Feld: "Eigentümer", Soll: "Sanierung und Kosten")
        };
        result["schachtfeldzuordnung_vorgabe_9_2"] = feldSoll.Select(f => new {
            feld = f.Feld, soll = f.Soll,
            ist = gruppenMethode.Invoke(null, new object[] { f.Feld })
        }).ToArray();
        var stamm = AuswertungPro.Next.UI.DataPage.DataPageColumnViewCatalog.Resolve("stammdaten");
        result["haltung_stammdaten_vorgabe_4_3"] = new {
            anzahl = stamm.Felder!.Count,
            inspektionsrichtungVorhanden = stamm.Enthaelt("Inspektionsrichtung"),
            sollAnzahl = 14
        };
        try
        {
            var page = new AuswertungPro.Next.UI.Views.Pages.MediaConflictsPage();
            page.Measure(new Size(1600, 900));
            page.Arrange(new Rect(0, 0, 1600, 900));
            page.UpdateLayout();
            result["medienkonflikte_mit_echtem_app_baml"] = "erfolgreich";
        }
        catch (Exception ex) { result["medienkonflikte_mit_echtem_app_baml"] = ex.ToString(); }
        File.WriteAllText(Path.Combine(Root, "ressourcenprobe.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }
}
