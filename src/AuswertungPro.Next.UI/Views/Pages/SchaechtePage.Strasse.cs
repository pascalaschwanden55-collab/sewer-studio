using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Uebertraegt die Strasse von den angrenzenden Haltungen auf den Schacht.
/// Ober- und Unterschacht liegen an derselben Stelle wie die Leitung, also
/// gilt dort dieselbe Adresse.
///
/// Kein Netzzugriff und keine amtliche Auskunft — nur eine Uebertragung im
/// eigenen Projekt. Geschrieben wird trotzdem erst nach Bestaetigung und nur
/// in ein leeres Feld.
/// </summary>
public partial class SchaechtePage
{
    private static readonly IStrassenUebernahme Strassen = new StrassenUebernahme();

    private const string StrassenTitel = "Strasse übernehmen";

    private ICommand? BaueStrassenBefehl(SchachtRecord record, string feldname)
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

    private void UebernimmStrasse(SchachtRecord record)
    {
        var nummer = record.GetFieldValue(StrassenUebernahmeMapper.SchachtnummerFeld);
        if (string.IsNullOrWhiteSpace(nummer))
        {
            Dialogs.Info("Ohne Schachtnummer lässt sich kein Nachbar bestimmen.", StrassenTitel);
            return;
        }

        var ergebnis = Strassen.FuerSchacht(
            nummer,
            StrassenUebernahmeMapper.Haltungen(_vm?.Project?.Data));

        var fenster = new FeldVorschlagWindow(
            nummer, StrassenUebernahmeMapper.StrassenFeld, ergebnis)
        {
            Owner = Window.GetWindow(this)
        };

        if (fenster.ShowDialog() != true || fenster.Uebernommen is null)
            return;

        SchreibeStrasse(record, fenster.Uebernommen.Wert);
        MarkProjectDirty();
        ApplySearchFilter();
    }

    /// <summary>
    /// Schreibt den bestaetigten Wert. userEdited: true ist Pflicht — es ist
    /// der einzige Schutz davor, dass der naechste Import ihn ueberschreibt.
    /// Unmittelbar davor wird noch einmal geprueft, ob das Feld inzwischen
    /// gefuellt ist: Der Menuepunkt erscheint zwar nur an leeren Feldern,
    /// aber zwischen Anzeige und Klick kann sich das aendern.
    /// </summary>
    private bool SchreibeStrasse(SchachtRecord record, string wert)
    {
        if (_vm is null || !_vm.CanMutateRecord(record, "Strasse übernehmen"))
            return false;

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
    /// Der Stapellauf fuer die ganze Liste. Er zeigt zuerst die Vorschau und
    /// schreibt erst nach Bestaetigung; mehrdeutige Schaechte bleiben darin
    /// sichtbar, werden aber nicht uebernommen.
    /// </summary>
    private void StrassenStapel()
    {
        var schaechte = StrassenUebernahmeMapper.Schaechte(_vm?.Records);
        var haltungen = StrassenUebernahmeMapper.Haltungen(_vm?.Project?.Data);

        var fenster = new StrassenUebernahmeWindow(
            "Strassen aus den Haltungen übernehmen",
            "Schacht",
            Strassen.AlleSchaechte(schaechte, haltungen),
            Strassen.MehrdeutigeSchaechte(schaechte, haltungen))
        {
            Owner = Window.GetWindow(this)
        };

        if (fenster.ShowDialog() != true)
            return;

        var geschrieben = 0;
        foreach (var zeile in fenster.Gewaehlt)
        {
            var record = _vm?.Records.FirstOrDefault(r => string.Equals(
                r.GetFieldValue(StrassenUebernahmeMapper.SchachtnummerFeld)?.Trim(),
                zeile.Nummer,
                StringComparison.OrdinalIgnoreCase));

            if (record is not null && SchreibeStrasse(record, zeile.Wert))
                geschrieben++;
        }

        if (geschrieben > 0)
        {
            MarkProjectDirty();
            ApplySearchFilter();
        }

        Dialogs.Info(
            geschrieben == 1
                ? "1 Schacht hat die Strasse seiner Haltung übernommen."
                : $"{geschrieben} Schächte haben die Strasse ihrer Haltung übernommen.",
            StrassenTitel);
    }
}
