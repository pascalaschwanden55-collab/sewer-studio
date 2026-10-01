using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Objektakten;

/// <summary>Anzeige aus explizitem Hauptdeckel, sonst genau einem zugehoerigen Deckel. Kein Schreibvorgang.</summary>
public static class SchachtDeckelAnzeige
{
    public static ObjektAkte? Waehle(Project projekt, ObjektAkte schacht)
    {
        var deckel = projekt.Objektakten.Where(a => a.Art == "deckel" && a.Bezuege.Contains(schacht.Id)).ToArray();
        if (schacht.HauptdeckelId is { } id) return deckel.SingleOrDefault(d => d.Id == id);
        return deckel.Length == 1 ? deckel[0] : null;
    }
}
