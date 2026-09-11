using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Objektakten;

/// <summary>Was die Fachperson an einer Auswahlliste geaendert hat.</summary>
public enum ListenErgaenzungArt
{
    /// <summary>Ein eigener Eintrag, den der WebGIS-Katalog nicht kennt.</summary>
    Hinzugefuegt,
    /// <summary>Ein WebGIS-Eintrag zeigt einen anderen Text; sein Code bleibt.</summary>
    Umbenannt,
    /// <summary>Ein WebGIS-Eintrag erscheint nicht mehr in der Liste; gespeicherte Werte bleiben lesbar.</summary>
    Ausgeblendet,
}

/// <summary>Eine Ergaenzung an einer Auswahlliste - programmweit, unabhaengig vom Projekt.
/// <paramref name="Eltern"/> ist der Code der Elterngruppe bei abhaengigen Listen, sonst leer.
/// Bei <see cref="ListenErgaenzungArt.Hinzugefuegt"/> ist <paramref name="Text"/> der Eintrag und
/// <paramref name="Code"/> optional; bei den anderen Arten bezeichnet <paramref name="Code"/> den
/// WebGIS-Eintrag.</summary>
public sealed record ListenErgaenzung(string KatalogId, string? Eltern, ListenErgaenzungArt Art, string? Code, string? Text)
{
    /// <summary>Wirft bei einer Ergaenzung, die nichts Sinnvolles bedeuten kann.</summary>
    public void Pruefe()
    {
        if (string.IsNullOrWhiteSpace(KatalogId))
            throw new ArgumentException("Eine Listenergänzung braucht die Kennung ihrer Liste.");
        switch (Art)
        {
            case ListenErgaenzungArt.Hinzugefuegt when string.IsNullOrWhiteSpace(Text):
                throw new ArgumentException("Ein eigener Eintrag braucht einen Text.");
            case ListenErgaenzungArt.Umbenannt when string.IsNullOrWhiteSpace(Code) || string.IsNullOrWhiteSpace(Text):
                throw new ArgumentException("Umbenennen braucht den Code des Eintrags und den neuen Text.");
            case ListenErgaenzungArt.Ausgeblendet when string.IsNullOrWhiteSpace(Code):
                throw new ArgumentException("Ausblenden braucht den Code des Eintrags.");
        }
    }
}

/// <summary>Programmweiter Speicher der Listenergaenzungen (AppData). Eine fehlende Datei ist
/// leer; eine vorhandene, aber unlesbare Datei bricht ab, statt still ohne Ergaenzungen zu laufen.</summary>
public interface IObjektaktenListenErgaenzungen
{
    IReadOnlyList<ListenErgaenzung> Lade();
    void Speichere(IEnumerable<ListenErgaenzung> ergaenzungen);
}

/// <summary>Legt die Ergaenzungen ueber die Eintraege eines Katalogs. Reine Regel, kein Zustand.</summary>
public static class ListenErgaenzungRegel
{
    /// <summary>Position, ab der eigene Eintraege gezaehlt werden - weit weg von jedem Katalogindex,
    /// damit ein gespeicherter <c>LokalerEintrag</c> nie auf einen WebGIS-Eintrag zeigt.</summary>
    public const int EigeneAbPosition = 10000;

    public static IReadOnlyList<ObjektAuswahl> Anwenden(IEnumerable<ObjektAuswahl> basis, string katalogId, string? eltern,
        IReadOnlyList<ListenErgaenzung> ergaenzungen)
    {
        var passend = ergaenzungen.Where(e => e.KatalogId == katalogId && (e.Eltern ?? "") == (eltern ?? "")).ToArray();
        if (passend.Length == 0) return basis.ToArray();

        var ausgeblendet = passend.Where(e => e.Art == ListenErgaenzungArt.Ausgeblendet).Select(e => e.Code).ToHashSet();
        var umbenannt = passend.Where(e => e.Art == ListenErgaenzungArt.Umbenannt)
            .GroupBy(e => e.Code!).ToDictionary(g => g.Key, g => g.Last().Text!);

        var ergebnis = new List<ObjektAuswahl>();
        foreach (var eintrag in basis)
        {
            if (eintrag.OriginalCode is not null && ausgeblendet.Contains(eintrag.OriginalCode)) continue;
            ergebnis.Add(eintrag.OriginalCode is not null && umbenannt.TryGetValue(eintrag.OriginalCode, out var text)
                ? eintrag with { Label = text }
                : eintrag);
        }
        var lauf = EigeneAbPosition;
        foreach (var eigen in passend.Where(e => e.Art == ListenErgaenzungArt.Hinzugefuegt))
        {
            var code = string.IsNullOrWhiteSpace(eigen.Code) ? null : eigen.Code.Trim();
            ergebnis.Add(new ObjektAuswahl(lauf++, code, eigen.Text!.Trim()) { Eltern = eltern, Eigen = true });
        }
        return ergebnis;
    }

    /// <summary>Ein Eintrag ist dann ein eigener, wenn er so markiert ist und aus der
    /// Ergaenzungsliste stammt - nicht, weil jemand die Markierung setzt.</summary>
    public static bool IstEigener(ObjektAuswahl auswahl, string katalogId, string? eltern, IReadOnlyList<ListenErgaenzung> ergaenzungen)
        => auswahl.Eigen && ergaenzungen.Any(e => e.Art == ListenErgaenzungArt.Hinzugefuegt
            && e.KatalogId == katalogId && (e.Eltern ?? "") == (eltern ?? "")
            && string.Equals(e.Text?.Trim(), auswahl.Label, StringComparison.Ordinal)
            && string.Equals(string.IsNullOrWhiteSpace(e.Code) ? null : e.Code.Trim(), auswahl.OriginalCode, StringComparison.Ordinal));
}
