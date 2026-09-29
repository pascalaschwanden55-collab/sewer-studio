using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.UseCases.ProjektPruefung;

/// <summary>
/// Kopiert genau die Daten, welche die fünf Prüfregeln lesen. Jeder Schritt liest
/// Live-Daten nur auf dem aufrufenden Thread; zwischen den kleinen Abschnitten darf
/// dessen Ereignisschleife wieder arbeiten.
/// </summary>
public static class ProjektPruefdatenKopie
{
    private const int Abschnitt = 64;

    public static async Task<Project> ErfasseAsync(Project quelle, Func<Task> weiter, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(quelle);
        ArgumentNullException.ThrowIfNull(weiter);
        var kopie = new Project { Id = quelle.Id, CreatedAtUtc = DateTime.MinValue,
            ModifiedAtUtc = DateTime.MinValue, Data = new(), SchaechteData = new(), Objektakten = [] };
        var schritte = 0;
        async Task Pause()
        {
            ct.ThrowIfCancellationRequested();
            if (++schritte % Abschnitt == 0) await weiter();
        }

        // Die Reihenfolge ist Teil des Ergebnisses. Bei Änderungen während der
        // Erfassung verwirft der Aufrufer die Kopie nach einem zweiten Durchgang.
        foreach (var h in quelle.Data)
        {
            ct.ThrowIfCancellationRequested();
            var ziel = new HaltungRecord
            {
                Id = h.Id, CreatedAtUtc = DateTime.MinValue, ModifiedAtUtc = DateTime.MinValue,
                Fields = new(h.Fields, StringComparer.Ordinal), FieldMeta = new(StringComparer.Ordinal)
            };
            kopie.Data.Add(ziel);
            var eintraege = h.Protocol?.Current?.Entries;
            if (eintraege is not null)
            {
                ziel.Protocol = LeeresProtokoll();
                foreach (var e in eintraege)
                {
                    ziel.Protocol.Current.Entries.Add(new ProtocolEntry
                    {
                        EntryId = e.EntryId, Code = e.Code, MeterStart = e.MeterStart,
                        MeterEnd = e.MeterEnd, IsDeleted = e.IsDeleted,
                        Ai = e.Ai is null ? null : new ProtocolEntryAiMeta
                        { Accepted = e.Ai.Accepted, SuggestedAt = DateTimeOffset.MinValue }
                    });
                    await Pause();
                }
            }
            await Pause();
        }
        foreach (var s in quelle.SchaechteData)
        {
            ct.ThrowIfCancellationRequested();
            var ziel = new SchachtRecord
            {
                Id = s.Id, CreatedAtUtc = DateTime.MinValue, ModifiedAtUtc = DateTime.MinValue,
                Fields = new(s.Fields, StringComparer.Ordinal),
                FieldMeta = s.FieldMeta.ToDictionary(x => x.Key,
                    x => new FieldMetadata { FieldName = x.Key, UserEdited = x.Value.UserEdited,
                        LastUpdatedUtc = DateTime.MinValue }, StringComparer.Ordinal),
                Geonis = s.Geonis is null ? null : new GeonisKennungen { Knoten = s.Geonis.Knoten }
            };
            kopie.SchaechteData.Add(ziel);
            await Pause();
        }
        foreach (var a in quelle.Objektakten)
        {
            ct.ThrowIfCancellationRequested();
            var ziel = new ObjektAkte { Id = a.Id, Art = a.Art, HauptdeckelId = a.HauptdeckelId,
                Bezuege = new(a.Bezuege), Werte = new(StringComparer.Ordinal), Quellen = [] };
            kopie.Objektakten.Add(ziel);
            foreach (var (key, wert) in a.Werte)
            {
                ziel.Werte[key] = new ObjektFeldWert { Text = wert.Text, VonHand = wert.VonHand };
                await Pause();
            }
            foreach (var q in a.Quellen)
            {
                ziel.Quellen.Add(new ObjektQuellbeleg
                {
                    Klasse = q.Klasse, Kennung = q.Kennung, ImportiertUtc = q.ImportiertUtc,
                    Werte = new(q.Werte), Referenzen = new(q.Referenzen)
                });
                await Pause();
            }
            await Pause();
        }
        ct.ThrowIfCancellationRequested();
        return kopie;
    }

    private static ProtocolDocument LeeresProtokoll() => new()
    {
        Original = new() { RevisionId = Guid.Empty, CreatedAt = DateTimeOffset.MinValue },
        Current = new() { RevisionId = Guid.Empty, CreatedAt = DateTimeOffset.MinValue },
        History = []
    };

    /// <summary>
    /// Kurzer, durchgehender Abgleich auf dem UI-Thread. Er liest dieselben Quellen
    /// wie ErfasseAsync, ohne JSON, Dateioperationen oder Wartepunkt.
    /// </summary>
    public static bool Gleich(Project stand, Project live)
    {
        if (stand.Id != live.Id || stand.Data.Count != live.Data.Count
            || stand.SchaechteData.Count != live.SchaechteData.Count
            || stand.Objektakten.Count != live.Objektakten.Count) return false;
        for (var i = 0; i < stand.Data.Count; i++)
        {
            var a = stand.Data[i]; var b = live.Data[i];
            if (a.Id != b.Id || !Gleich(a.Fields, b.Fields)) return false;
            var ae = a.Protocol?.Current?.Entries;
            var be = b.Protocol?.Current?.Entries;
            var anzahl = ae?.Count ?? 0;
            if (anzahl != (be?.Count ?? 0)) return false;
            for (var j = 0; j < anzahl; j++)
            {
                var x = ae![j]; var y = be![j];
                if (x.EntryId != y.EntryId || x.Code != y.Code
                    || !Nullable.Equals(x.MeterStart, y.MeterStart)
                    || !Nullable.Equals(x.MeterEnd, y.MeterEnd) || x.IsDeleted != y.IsDeleted
                    || (x.Ai is null) != (y.Ai is null)
                    || x.Ai?.Accepted != y.Ai?.Accepted) return false;
            }
        }
        for (var i = 0; i < stand.SchaechteData.Count; i++)
        {
            var a = stand.SchaechteData[i]; var b = live.SchaechteData[i];
            if (a.Id != b.Id || !Gleich(a.Fields, b.Fields)
                || a.Geonis?.Knoten != b.Geonis?.Knoten
                || a.FieldMeta.Count != b.FieldMeta.Count) return false;
            foreach (var (key, meta) in a.FieldMeta)
                if (!b.FieldMeta.TryGetValue(key, out var aktuell)
                    || meta.UserEdited != aktuell.UserEdited) return false;
        }
        for (var i = 0; i < stand.Objektakten.Count; i++)
        {
            var a = stand.Objektakten[i]; var b = live.Objektakten[i];
            if (a.Id != b.Id || a.Art != b.Art || a.HauptdeckelId != b.HauptdeckelId
                || !a.Bezuege.SequenceEqual(b.Bezuege) || a.Werte.Count != b.Werte.Count
                || a.Quellen.Count != b.Quellen.Count) return false;
            foreach (var (key, wert) in a.Werte)
                if (!b.Werte.TryGetValue(key, out var aktuell)
                    || wert.Text != aktuell.Text || wert.VonHand != aktuell.VonHand) return false;
            for (var j = 0; j < a.Quellen.Count; j++)
            {
                var x = a.Quellen[j]; var y = b.Quellen[j];
                if (x.Klasse != y.Klasse || x.Kennung != y.Kennung
                    || x.ImportiertUtc != y.ImportiertUtc || !Gleich(x.Werte, y.Werte)
                    || !Gleich(x.Referenzen, y.Referenzen)) return false;
            }
        }
        return true;
    }

    private static bool Gleich(Dictionary<string, string> stand, Dictionary<string, string> live)
    {
        if (stand.Count != live.Count) return false;
        using var links = stand.GetEnumerator();
        using var rechts = live.GetEnumerator();
        while (links.MoveNext())
        {
            if (!rechts.MoveNext() || links.Current.Key != rechts.Current.Key
                || links.Current.Value != rechts.Current.Value) return false;
        }
        return true;
    }
}
