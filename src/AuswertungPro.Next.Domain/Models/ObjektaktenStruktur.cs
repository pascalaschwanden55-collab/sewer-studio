namespace AuswertungPro.Next.Domain.Models;

public static class ObjektaktenStruktur
{
    public static void Pruefe(Project projekt)
    {
        var akten = projekt.Objektakten;
        if (akten is null || akten.Any(a => a is null || a.Id == Guid.Empty || a.Werte is null || a.Bezuege is null
                || a.Quellen is null || a.Unterlisten is null || a.Werte.Any(f => f.Value is null || f.Value.Text is null)
                || a.Quellen.Any(q => q is null || q.Werte is null || q.Referenzen is null || q.Strukturen is null
                    || q.Modell is null || q.Klasse is null || q.Kennung is null || q.System is null
                    || q.Werte.Values.Any(v => v is null) || q.Referenzen.Values.Any(v => v is null) || q.Strukturen.Values.Any(v => v is null))
                || a.Unterlisten.Any(l => l.Value is null || l.Value.Any(z => z is null)))
            || akten.Select(a => a.Id).Distinct().Count() != akten.Count)
            throw new InvalidOperationException("Die Objektakten sind beschädigt oder enthalten doppelte Kennungen. Keine Daten wurden verworfen.");
    }
}
