using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Services;

/// <summary>Dateiauswahl und abbrechbare Vorschau. Fachliche Entscheidungen liegen im Application-Planer.</summary>
public sealed class GeoShopAbgleichDialog(IGeoShopLeser leser, IDialogService dialogs, Action<string>? merkeDatei = null)
{
    public int Zeige(BauteilArt art, Func<IReadOnlyList<GeoShopZiel>> ziele, Func<bool> darfSchreiben)
    {
        if (!darfSchreiben()) return 0;
        var dateien = dialogs.OpenFiles("GeoShop-XTF und bei Bedarf Eigentümer-JSON gemeinsam auswählen", "GeoShop-Dateien (*.xtf;*.json)|*.xtf;*.json");
        if (dateien.Length == 0) return 0;
        var xtf = dateien.Where(p => Path.GetExtension(p).Equals(".xtf", StringComparison.OrdinalIgnoreCase)).ToArray();
        var json = dateien.Where(p => Path.GetExtension(p).Equals(".json", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (xtf.Length != 1 || json.Length > 1 || xtf.Length + json.Length != dateien.Length)
        { dialogs.Error("Bitte genau eine XTF und höchstens eine Eigentümer-JSON auswählen."); return 0; }
        var datei = xtf[0];
        merkeDatei?.Invoke(datei);
        var original = ziele();
        var namen = original.Select(z => z.Name).ToArray();
        using var abbruch = new CancellationTokenSource();
        GeoShopPlan? plan = null;
        var fenster = new GeoShopAbgleichWindow();
        var owner = System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        if (owner is not null) fenster.Owner = owner;
        fenster.Closed += (_, _) => abbruch.Cancel();
        fenster.Loaded += async (_, _) =>
        {
            try
            {
                var bestand = await Task.Run(() => leser.Lies(datei, art, namen, abbruch.Token), abbruch.Token);
                if (json.Length == 1)
                    bestand = GeoShopEigentuemerErgaenzung.Ergaenze(bestand, leser.LiesEigentuemer(json[0]), json[0]);
                if (abbruch.IsCancellationRequested) return;
                plan = GeoShopAbgleichPlanBuilder.Baue(original, bestand);
                fenster.Zeige(GeoShopAbgleichBericht.Schreibe(plan), plan.Positionen.Count > 0);
            }
            catch (OperationCanceledException) when (abbruch.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                if (!abbruch.IsCancellationRequested)
                    fenster.Zeige($"Die XTF konnte nicht abgeglichen werden.\n\n{ex.Message}", false);
            }
        };
        if (fenster.ShowDialog() != true || plan is null || !darfSchreiben()) return 0;
        try { return GeoShopAbgleichAnwender.WendeAn(plan, ziele()); }
        catch (Exception ex) { dialogs.Error(ex.Message, "GeoShop-Abgleich"); return 0; }
    }
}
