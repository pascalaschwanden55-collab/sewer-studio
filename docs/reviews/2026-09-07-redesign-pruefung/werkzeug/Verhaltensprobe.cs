using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Application.UseCases.CodingSuggestions;
using AuswertungPro.Next.Application.UseCases.Suche;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using AuswertungPro.Next.UI.Views.Pages.Schachtansicht;

internal static partial class Program
{
    static void PruefeVerhalten(ShellViewModel shell, AuswertungPro.Next.UI.ServiceProvider sp, Window window)
    {
        var result = new Dictionary<string, object>();
        var a = shell.Project;
        var treffer = GlobaleSucheRegel.Suche("Teststrasse", a.Data, a.SchaechteData, r => r.GetFieldValue("Schachtnummer") ?? "");
        result["suche_13_haltungen_6_schaechte"] = new { count = treffer.Count, arten = treffer.Select(t => t.Art.ToString()).ToArray() };

        shell.GlobaleSuche.Text = "10001-10002";
        var alterTreffer = shell.GlobaleSuche.Treffer.First();
        var b = new Project { Name = "Projekt B" };
        b.EnsureMetadataDefaults();
        var br = b.CreateNewRecord();
        br.SetFieldValue(FieldKeys.HoldingName, "10001-10002", FieldSource.Manual, false);
        b.AddRecord(br);
        b.Dirty = false;
        sp.CodingSuggestionRegistry.Merke("nur-in-Projekt-A", CodingSuggestionSet.Leer("aus Projekt A")); shell.ReplaceProject(b);
        shell.GlobaleSuche.WaehleErstenOderMarkierten();
        result["suchtreffer_nach_projektwechsel"] = new { projekt = shell.Project.Name, altTrefferNochVerwendet = shell.CurrentPage is DataPageViewModel dp && ReferenceEquals(dp.Selected, alterTreffer.Ziel), auswahlIstImAktivenProjekt = shell.CurrentPage is DataPageViewModel dp2 && b.Data.Contains(dp2.Selected!) };

        shell.NavigateTo("Uebersicht");
        var uebersicht = (ProjektUebersichtPageViewModel)shell.CurrentPage!;
        result["projektfremder_ki_lauf"] = new { projekt = shell.Project.Name, zeilen = uebersicht.KiLaeufe.Select(x => x.Haltung).ToArray() };

        var c = new Project { Name = "Projekt C" };
        c.EnsureMetadataDefaults();
        var cPath = Path.Combine(Root, "projektC", "Projektdateien", "projekt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(cPath)!);
        new AuswertungPro.Next.Infrastructure.Projects.JsonProjectRepository().Save(c, cPath);
        b.Dirty = false;
        uebersicht.ProjektOeffnenCommand.Execute(cPath);
        result["uebersicht_nach_projektwechsel"] = new { projekt = shell.Project.Name, angezeigterTitel = uebersicht.HeroTitel, angezeigteHaltungen = uebersicht.Kennzahlen?.Haltungen, echteHaltungen = shell.Project.Data.Count, selbeSeite = ReferenceEquals(shell.CurrentPage, uebersicht) };

        var record = new SchachtRecord();
        record.SetFieldValue(FieldKeys.ShaftShape, "Rechteckig", FieldSource.Manual, false);
        var panel = new SchachtUebersichtPanel { Record = record };
        result["rechteckiger_schacht"] = new { kreis = ((FrameworkElement)panel.FindName("Kreis")).Visibility.ToString(), oval = ((FrameworkElement)panel.FindName("Oval")).Visibility.ToString(), rechteck = ((FrameworkElement)panel.FindName("Quadrat")).Visibility.ToString() };

        File.WriteAllText(Path.Combine(Root, "verhaltensprobe.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }
}
