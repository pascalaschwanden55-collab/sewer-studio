using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.ViewModels.Pages;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Die Gegenrichtung zu <see cref="SchaechtePage"/>: Die Strasse der beiden
/// Schaechte gilt auch fuer die Haltung dazwischen.
///
/// Kein Netzzugriff und keine amtliche Auskunft — nur eine Uebertragung im
/// eigenen Projekt. Geschrieben wird trotzdem erst nach Bestaetigung und nur
/// in ein leeres Feld.
/// </summary>
public partial class DataPage
{
    private static readonly IStrassenUebernahme Strassen = new StrassenUebernahme();

    private const string StrassenTitel = "Strasse übernehmen";

    private ICommand? BaueStrassenBefehl(HaltungRecord record, string feldname)
    {
        if (!string.Equals(feldname?.Trim(), StrassenUebernahmeMapper.StrassenFeld,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new EinfacherBefehl(
            () => { UebernimmStrasse(record); return Task.CompletedTask; },
            fehler => Dialogs.Error(
                $"Die Übernahme ist fehlgeschlagen: {fehler.Message}", StrassenTitel));
    }

    private void UebernimmStrasse(HaltungRecord record)
    {
        var vm = DataContext as DataPageViewModel;
        var haltung = StrassenUebernahmeMapper.Haltungen([record]).Single();

        if (string.IsNullOrWhiteSpace(haltung.Name))
        {
            Dialogs.Info("Ohne Haltungsnamen lässt sich kein Nachbar bestimmen.", StrassenTitel);
            return;
        }

        var ergebnis = Strassen.FuerHaltung(
            haltung,
            StrassenUebernahmeMapper.Schaechte(vm?.Project?.SchaechteData));

        var fenster = new FeldVorschlagWindow(
            haltung.Name, StrassenUebernahmeMapper.StrassenFeld, ergebnis)
        {
            Owner = Window.GetWindow(this)
        };

        if (fenster.ShowDialog() != true || fenster.Uebernommen is null)
            return;

        if (SchreibeStrasse(record, fenster.Uebernommen.Wert))
            vm?.ScheduleAutoSave();
    }

    /// <summary>
    /// Schreibt den bestaetigten Wert. userEdited: true ist Pflicht — es ist
    /// der einzige Schutz davor, dass der naechste Import ihn ueberschreibt.
    /// Unmittelbar davor wird noch einmal geprueft, ob das Feld inzwischen
    /// gefuellt ist.
    /// </summary>
    private bool SchreibeStrasse(HaltungRecord record, string wert)
    {
        if (!string.IsNullOrWhiteSpace(record.GetFieldValue(StrassenUebernahmeMapper.StrassenFeld)))
        {
            Dialogs.Info(
                "Das Feld \"Strasse\" hat inzwischen einen Wert. Die Übernahme überschreibt nichts.",
                StrassenTitel);
            return false;
        }

        record.SetFieldValue(
            StrassenUebernahmeMapper.StrassenFeld, wert, FieldSource.Manual, userEdited: true);
        return true;
    }

    private void StrassenStapel_Click(object sender, RoutedEventArgs e) => StrassenStapel();

    /// <summary>
    /// Der Stapellauf fuer die ganze Liste. Vorschau zuerst, geschrieben wird
    /// erst nach Bestaetigung.
    /// </summary>
    private void StrassenStapel()
    {
        var vm = DataContext as DataPageViewModel;
        var haltungen = StrassenUebernahmeMapper.Haltungen(vm?.Records);
        var schaechte = StrassenUebernahmeMapper.Schaechte(vm?.Project?.SchaechteData);

        var fenster = new StrassenUebernahmeWindow(
            "Strassen aus den Schächten übernehmen",
            "Haltung",
            Strassen.AlleHaltungen(haltungen, schaechte),
            [])
        {
            Owner = Window.GetWindow(this)
        };

        if (fenster.ShowDialog() != true)
            return;

        var geschrieben = 0;
        foreach (var zeile in fenster.Gewaehlt)
        {
            var record = vm?.Records.FirstOrDefault(r => string.Equals(
                r.GetFieldValue(StrassenUebernahmeMapper.HaltungsnameFeld)?.Trim(),
                zeile.Nummer,
                StringComparison.OrdinalIgnoreCase));

            if (record is not null && SchreibeStrasse(record, zeile.Wert))
                geschrieben++;
        }

        if (geschrieben > 0)
            vm?.ScheduleAutoSave();

        Dialogs.Info(
            geschrieben == 1
                ? "1 Haltung hat die Strasse ihrer Schächte übernommen."
                : $"{geschrieben} Haltungen haben die Strasse ihrer Schächte übernommen.",
            StrassenTitel);
    }
}
