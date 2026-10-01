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
