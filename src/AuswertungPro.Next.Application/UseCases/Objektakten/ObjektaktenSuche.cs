using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Objektakten;

public sealed record ObjektAktenTreffer(ObjektAkte Akte, ObjektFeldDefinition Feld, string Wert, string Titel);

public static class ObjektaktenSuche
{
    public static IEnumerable<ObjektAktenTreffer> Finde(ObjektaktenBearbeitung b, string suche)
    {
        var woerter = suche.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (woerter.Length == 0) yield break;
        foreach (var a in b.Verbund)
            foreach (var f in ObjektaktenBestandsfelder.Fuer(b, a))
            {
                var wert = b.Lies(a, f);
                var text = f.Label + " " + wert + " " + f.Gruppe + " " + f.Speicherfeld + " " + a.Werte.GetValueOrDefault(f.Id)?.Originalcode;
                if (woerter.All(w => text.Contains(w, StringComparison.CurrentCultureIgnoreCase)))
                    yield return new(a, f, wert, $"{(a.Id == b.WurzelId ? b.Bezugsname(a.Id) : a.ToString())} · {f.Label}: {wert}");
            }
    }
}
