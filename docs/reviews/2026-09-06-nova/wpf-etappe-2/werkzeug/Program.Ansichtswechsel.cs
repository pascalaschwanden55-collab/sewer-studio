using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;
using AuswertungPro.Next.UI.Views.Pages.Schachtansicht;

internal static partial class Program
{
    // Echte Seite + echtes ViewModel im eigenen Profil, einschliesslich Menue-Rueckweg.
    static void PruefeAnsichtswechsel(Window window)
    {
        var seitenTyp = AppliedPage == "Schaechte" ? "SchaechtePage" : "DataPage";
        var seite = Descendants(window).OfType<FrameworkElement>().Single(e => e.GetType().Name == seitenTyp);
        var drawer = (HaltungFelderDrawer)seite.FindName("FelderDrawer");
        var liste = (FrameworkElement)seite.FindName("AufklappListe");
        var shell = (ShellViewModel)window.DataContext;
        var vorher = JsonSerializer.Serialize(shell.Project);
        object? Feld(object ziel, string name) => ziel.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(ziel);
        var workspace = Feld(seite, "_novaWorkspace") ?? throw new InvalidOperationException("Workspace fehlt");
        var controller = Feld(seite, "_aufklappListe") ?? throw new InvalidOperationException("Listencontroller fehlt");
        void Fordere(bool wahr, string grund) { if (!wahr) throw new InvalidOperationException(grund); }
        Fordere(drawer.Groups is null && Feld(workspace, "_felderSync") is null, "Unsichtbares Zweitformular in der Liste");
        Fordere(Feld(controller, "_sync") is not null, "Offene Liste ohne Live-Abgleich");
        var menue = (MenuItem)seite.FindName("AnsichtTabelleMenu");
        menue.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        window.UpdateLayout();
        Fordere(drawer.Groups?.Count > 0 && Feld(workspace, "_felderSync") is not null, "Tabellen-Rueckweg fuellt Formular nicht");
        Fordere(Feld(controller, "_sync") is null, "Listen-Abgleich bleibt in der Tabelle aktiv");
        Fordere(liste.Visibility == Visibility.Collapsed, "Liste bleibt in Tabelle sichtbar");
        menue = (MenuItem)seite.FindName("AnsichtListeMenu");
        menue.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        window.UpdateLayout();
        Fordere(drawer.Groups is null && Feld(workspace, "_felderSync") is null, "Tabellenformular nach Rueckkehr noch aktiv");
        Fordere(liste.Visibility == Visibility.Visible, "Liste nach Rueckkehr unsichtbar");
        Fordere(vorher == JsonSerializer.Serialize(shell.Project), "Ansichtswechsel veraendert Projektdaten");
        File.WriteAllText(Path.Combine(Root, $"ansichtswechsel-{AppliedPage}-{AppliedTheme}.json"),
            JsonSerializer.Serialize(new { erfolgreich = true, echteSeite = seitenTyp,
                genauEinAbgleich = true, rueckweg = true, projektdatenUnveraendert = true,
                profil = AuswertungPro.Next.UI.AppSettings.AppDataDir }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
