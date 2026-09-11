using System;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Services;

/// <summary>Dateiauswahl und Vorschau für den eigenen verlustfreien Zusatzweg.</summary>
public sealed class ObjektaktenPaketDialog(IObjektaktenPaketService service, IDialogService dialogs)
{
    public void Exportiere(Project projekt)
    {
        var datei = dialogs.SaveFile("Objektakten als Zusatzdatei exportieren", "Objektakten (*.json)|*.json", ".json",
            $"Objektakten-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        if (datei is null) return;
        try
        {
            service.Exportiere(projekt, datei);
            dialogs.Info("Zusatzdatei mit Objektakten, Bestandsfeldern und Herkunft gespeichert. Sie kann in dasselbe SewerStudio-Projekt eingelesen werden. Die GEONIS-Rückübernahme ist noch nicht bestätigt.", "Objektakten exportiert");
        }
        catch (Exception ex) { dialogs.Error(ex.Message, "Objektakten exportieren"); }
    }

    public bool Importiere(Project projekt, Func<bool> darfSchreiben)
    {
        var datei = dialogs.OpenFile("Objektakten-Zusatzdatei wählen", "Objektakten (*.json)|*.json");
        if (datei is null) return false;
        try
        {
            var paket = service.Lies(datei);
            var vorschau = ObjektaktenPaketImport.Pruefe(projekt, paket);
            if (!dialogs.Confirm(vorschau + "\n\nJetzt ergänzen?", "Objektakten übernehmen") || !darfSchreiben()) return false;
            ObjektaktenPaketImport.Uebernehme(projekt, paket);
            return true;
        }
        catch (Exception ex) { dialogs.Error(ex.Message, "Objektakten übernehmen"); return false; }
    }
}
