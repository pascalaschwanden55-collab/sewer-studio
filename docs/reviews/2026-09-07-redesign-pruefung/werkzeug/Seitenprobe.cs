using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using AuswertungPro.Next.UI.ViewModels;

internal static partial class Program
{
    static async Task PruefeSeiten(ShellViewModel shell, Window window)
    {
        var ergebnisse = new List<object>();
        foreach (var theme in new[] { "Light", "Dark" })
        {
            AppliedTheme = theme;
            System.Windows.Application.Current.Resources.MergedDictionaries[0] = new ResourceDictionary
            {
                Source = new Uri($"/SewerStudio;component/Theme/{(theme == "Dark" ? "Theme.xaml" : "ThemeLight.xaml")}", UriKind.Relative)
            };
            foreach (var nav in shell.NavItems)
            {
                AppliedPage = nav.Title;
                try
                {
                    shell.EnterWorkspaceOn(nav.Title);
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    window.UpdateLayout();
                    window.Background = (System.Windows.Media.Brush)window.FindResource("BgBrush"); await Task.Delay(650);
                    Foto(window, Path.Combine(Root, "bilder", $"{theme}-{nav.Title}.png"));
                    ergebnisse.Add(new { theme, page = nav.Title, viewModel = shell.CurrentPage?.GetType().Name, ok = true });
                }
                catch (Exception ex) { ergebnisse.Add(new { theme, page = nav.Title, ok = false, error = ex.ToString() }); }
            }
        }
        foreach (var page in new[] { "Haltungen", "Schaechte", "Import", "Einstellungen", "Uebersicht" })
        {
            window.Width = 1280; window.Height = 720;
            AppliedPage = page;
            shell.EnterWorkspaceOn(page);
            if (shell.CurrentPage is AuswertungPro.Next.UI.ViewModels.Pages.DataPageViewModel dp)
                dp.Selected = shell.Project.Data.First();
            if (shell.CurrentPage is AuswertungPro.Next.UI.ViewModels.Pages.SchaechtePageViewModel sp)
                sp.Selected = shell.Project.SchaechteData.First();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            window.UpdateLayout(); await Task.Delay(650);
            Foto(window, Path.Combine(Root, "bilder", $"Dark-{page}-1280x720.png"));
            Messe(window, window);
        }
        File.WriteAllText(Path.Combine(Root, "seitenprobe.json"), JsonSerializer.Serialize(ergebnisse, new JsonSerializerOptions { WriteIndented = true }));
    }
}
