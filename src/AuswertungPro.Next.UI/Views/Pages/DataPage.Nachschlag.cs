using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Pages;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Nachschlagen leerer Haltungsfelder beim Kanton. Getrennt von der uebrigen
/// Seitenlogik, weil es eine eigene Verantwortung ist: fragen, zeigen,
/// uebernehmen — und dabei nie einen vorhandenen Wert anfassen.
/// </summary>
public partial class DataPage
{
    // Alle Nachschlag-Befehle der Seite teilen sich diese Sperre: immer nur
    // eine Abfrage zur Zeit.
    private readonly NachschlagTor _nachschlagTor = new();

    /// <summary>
    /// Baut den Befehl "Beim Kanton nachschlagen" fuer genau ein Feld. Der
    /// Befehl fragt die Quelle, zeigt das Ergebnis und schreibt erst nach
    /// ausdruecklicher Bestaetigung.
    /// </summary>
    private ICommand? BaueNachschlagBefehl(HaltungRecord record, string feldname)
    {
        if (Vm?.FeldNachschlag is null)
            return null;
        if (FeldQuellenTabelle.QuelleFuer(feldname, BauteilArt.Haltung) is null)
            return null;

        return new EinfacherBefehl(
            async () => await NachschlagenAsync(record, feldname).ConfigureAwait(true),
            fehler => Dialogs.Error(
                $"Das Nachschlagen ist fehlgeschlagen: {fehler.Message}",
                "Beim Kanton nachschlagen"),
            _nachschlagTor);
    }

    private async Task NachschlagenAsync(HaltungRecord record, string feldname)
    {
        var useCase = Vm?.FeldNachschlag;
        if (useCase is null)
            return;

        var haltungsname = record.GetFieldValue("Haltungsname");
        if (string.IsNullOrWhiteSpace(haltungsname))
        {
            Dialogs.Info(
                "Ohne Haltungsnamen laesst sich beim Kanton nichts nachschlagen.",
                "Beim Kanton nachschlagen");
            return;
        }

        FeldNachschlagErgebnis ergebnis;
        try
        {
            ergebnis = await useCase
                .SucheAsync(new FeldNachschlagAnfrage(haltungsname, feldname, BauteilArt.Haltung))
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ergebnis = new FeldNachschlagErgebnis.Fehler(ex.Message);
        }

        var fenster = new FeldVorschlagWindow(haltungsname, feldname, ergebnis)
        {
            Owner = Window.GetWindow(this)
        };

        if (fenster.ShowDialog() != true || fenster.Uebernommen is null)
            return;

        UebernimmNachschlag(record, feldname, fenster.Uebernommen);
    }

    /// <summary>
    /// Schreibt den bestaetigten Wert. userEdited: true ist Pflicht — es ist
    /// der einzige Schutz davor, dass der naechste Import ihn ueberschreibt
    /// (die Merge-Prioritaet schuetzt nicht, sie waere sogar niedriger).
    /// </summary>
    private void UebernimmNachschlag(HaltungRecord record, string feldname, FeldVorschlag vorschlag)
    {

        // Zweite Sperre: Was importiert oder von Hand eingetragen wurde, darf
        // der Nachschlag nicht veraendern. Der Menuepunkt erscheint zwar nur
        // an leeren Feldern, aber geschrieben wird mit userEdited: true - das
        // umgeht den Handwert-Schutz des Datensatzes bewusst. Deshalb hier
        // unmittelbar vor dem Schreiben noch einmal pruefen.
        if (!string.IsNullOrWhiteSpace(record.GetFieldValue(feldname)))
        {
            Dialogs.Info(
                $"Das Feld \"{feldname}\" hat inzwischen einen Wert. "
                + "Der Nachschlag ueberschreibt nichts.",
                "Beim Kanton nachschlagen");
            return;
        }

        var herkunft = vorschlag.Herkunftshinweis switch
        {
            "Kataster" => FieldSource.Kataster,
            "Grundbuch" => FieldSource.Grundbuch,
            _ => FieldSource.Manual
        };

        record.SetFieldValue(feldname, vorschlag.Wert, herkunft, userEdited: true);

        // Derselbe Abschluss wie beim normalen Bearbeiten des Feldes.
        var vm = DataContext as DataPageViewModel;
        if (vm is not null)
        {
            vm.EnsureOptionForField(feldname, vorschlag.Wert);
            vm.ScheduleAutoSave();
        }
    }
}
