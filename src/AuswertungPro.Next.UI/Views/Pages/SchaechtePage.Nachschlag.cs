using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Views.Windows;
using static AuswertungPro.Next.UI.DataPage.SchaechteColumnPolicy;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Nachschlagen leerer Schachtfelder beim Kanton. Getrennt von der uebrigen
/// Seitenlogik, weil es eine eigene Verantwortung ist: fragen, zeigen,
/// uebernehmen — und dabei nie einen vorhandenen Wert anfassen.
/// </summary>
public partial class SchaechtePage
{
    /// <summary>
    /// Baut den Befehl "Beim Kanton nachschlagen" fuer genau ein Feld. Der
    /// Befehl fragt die Quelle, zeigt das Ergebnis und schreibt erst nach
    /// ausdruecklicher Bestaetigung.
    /// </summary>
    private ICommand? BaueNachschlagBefehl(SchachtRecord record, string feldname)
    {
        if (_vm?.FeldNachschlag is null)
            return null;
        if (FeldQuellenTabelle.QuelleFuer(feldname) is null)
            return null;

        return new EinfacherBefehl(
            async () => await NachschlagenAsync(record, feldname).ConfigureAwait(true),
            fehler => Dialogs.Error(
                $"Das Nachschlagen ist fehlgeschlagen: {fehler.Message}",
                "Beim Kanton nachschlagen"),
            _nachschlagTor);
    }

    private async Task NachschlagenAsync(SchachtRecord record, string feldname)
    {
        var useCase = _vm?.FeldNachschlag;
        if (useCase is null)
            return;

        var schachtnummer = record.GetFieldValue("Schachtnummer");
        if (string.IsNullOrWhiteSpace(schachtnummer))
        {
            _vm?.Dialogs?.Info(
                "Ohne Schachtnummer laesst sich beim Kanton nichts nachschlagen.",
                "Beim Kanton nachschlagen");
            return;
        }

        FeldNachschlagErgebnis ergebnis;
        try
        {
            ergebnis = await useCase
                .SucheAsync(new FeldNachschlagAnfrage(schachtnummer, feldname))
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ergebnis = new FeldNachschlagErgebnis.Fehler(ex.Message);
        }

        var fenster = new FeldVorschlagWindow(schachtnummer, feldname, ergebnis)
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
    private void UebernimmNachschlag(SchachtRecord record, string feldname, FeldVorschlag vorschlag)
    {
        if (_vm is null || !_vm.CanMutateRecord(record, "Feld beim Kanton nachschlagen"))
            return;

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

        // Ein neuer Eigentuemer soll auch in der Auswahlliste erscheinen —
        // derselbe Weg wie beim normalen Bearbeiten des Feldes.
        var optionField = ResolveOptionField(feldname);
        if (!string.IsNullOrWhiteSpace(optionField))
            _vm.EnsureOptionForField(optionField, vorschlag.Wert);

        MarkProjectDirty();
        ApplySearchFilter();
    }
}
