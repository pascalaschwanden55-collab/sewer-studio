using System;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>
/// Ein Objekt (mit seinen Massnahmen) aus dem gezeigten Plan herausnehmen oder durch einen frisch geprüften Stand
/// ersetzen — für «nur dieses Objekt prüfen/schreiben», ohne die ganze Liste neu zu lesen (28.09.2026).
/// </summary>
public static class WebGisPlanAusschnitt
{
    /// <summary>Neuer Plan mit genau diesem Objekt und seinen Massnahmen (dieselben Instanzen).</summary>
    public static WebGisExportPlan Von(WebGisExportPlan plan, WebGisObjektart art, Guid recordId)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var teil = new WebGisExportPlan();
        teil.Positionen.AddRange(plan.Positionen.FindAll(p => p.Objektart == art && p.RecordId == recordId));
        teil.Sanierungen.AddRange(plan.Sanierungen.FindAll(s => s.Objektart == art && s.ElternRecordId == recordId));
        return teil;
    }

    /// <summary>
    /// Ersetzt im Plan das Objekt (und seine Massnahmen) durch den Stand aus <paramref name="einzel"/>, an derselben Stelle.
    /// War es nicht im Plan, wird es angehängt.
    /// </summary>
    public static void Ersetze(WebGisExportPlan plan, WebGisExportPlan einzel, WebGisObjektart art, Guid recordId)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(einzel);

        var stelle = plan.Positionen.FindIndex(p => p.Objektart == art && p.RecordId == recordId);
        plan.Positionen.RemoveAll(p => p.Objektart == art && p.RecordId == recordId);
        var neu = einzel.Positionen.FindAll(p => p.Objektart == art && p.RecordId == recordId);
        if (stelle < 0 || stelle > plan.Positionen.Count) plan.Positionen.AddRange(neu);
        else plan.Positionen.InsertRange(stelle, neu);

        var sanStelle = plan.Sanierungen.FindIndex(s => s.Objektart == art && s.ElternRecordId == recordId);
        plan.Sanierungen.RemoveAll(s => s.Objektart == art && s.ElternRecordId == recordId);
        var neueSan = einzel.Sanierungen.FindAll(s => s.Objektart == art && s.ElternRecordId == recordId);
        if (sanStelle < 0 || sanStelle > plan.Sanierungen.Count) plan.Sanierungen.AddRange(neueSan);
        else plan.Sanierungen.InsertRange(sanStelle, neueSan);
    }
}
