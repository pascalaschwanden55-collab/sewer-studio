using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Application.UseCases.Xtf;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Objektakten;

public static class ObjektaktenExportBegleitung
{
    public static XtfExportVorschau Vorschau(Project projekt, XtfExportVorschau vorschau)
    {
        if (projekt.Objektakten.Count == 0) return vorschau;
        var dss = vorschau.Details.Contains("DSS_2020_1_LV95", StringComparison.Ordinal);
        var text = $"{projekt.Objektakten.Count} Objektakten werden zusätzlich als JSON ausgegeben. " +
            (dss ? "Belegte DSS-Felder, Deckel und Ereignisse stehen auch als Normobjekte in der neuen XTF. Offene Zuordnungen nennt der Exportbericht. "
                : "Dieser Aktualisierungsweg übernimmt zusätzliche Aktenwerte nicht als neue Normobjekte; sie bleiben in der Zusatzdatei erhalten. ") +
            "Die tatsächliche GEONIS-Rückübernahme muss am Zielsystem geprüft werden.";
        return vorschau with { Warnungen = new[] { text }.Concat(vorschau.Warnungen).ToArray(), Details = text + "\n\n" + vorschau.Details };
    }

    public static string Schreibe(IObjektaktenPaketService service, Project projekt, string? ordner)
    {
        if (projekt.Objektakten.Count == 0) return "";
        if (string.IsNullOrWhiteSpace(ordner)) throw new InvalidOperationException("Der XTF-Ausgabeordner fehlt.");
        var ziel = Path.Combine(ordner, $"Objektakten-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
        service.Exportiere(projekt, ziel);
        return "\nObjektakten und Übertragungsbericht: " + Path.GetFileName(ziel);
    }
}
