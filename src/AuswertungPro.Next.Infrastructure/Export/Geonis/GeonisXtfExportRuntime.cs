using AuswertungPro.Next.Application.Export.Geonis;
using AuswertungPro.Next.Application.UseCases;

namespace AuswertungPro.Next.Infrastructure.Export.Geonis;

/// <summary>
/// Einziger Aufbaupunkt fuer den GEONIS-Rueckschrieb. Werkzeug und spaeter die Oberflaeche
/// bauen den Ablauf nicht selbst zusammen, sondern holen ihn hier.
/// </summary>
public static class GeonisXtfExportRuntime
{
    public static GeonisXtfExportWorkflow Erzeuge()
        => new(
            new Sia405KatasterIndexReader(),
            new Sia405ExportPlanBuilder(),
            new Sia405ObjektQuelltextLeser(),
            new Sia405XtfWriter(),
            new Sia405ExportProtokollWriter());
}
