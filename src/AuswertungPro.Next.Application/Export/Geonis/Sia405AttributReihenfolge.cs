namespace AuswertungPro.Next.Application.Export.Geonis;

/// <summary>
/// Lernt die Reihenfolge der Attribut-Elemente je Klasse aus der Kataster-Datei.
///
/// Hintergrund: In INTERLIS-2-Transferdateien stehen die Attribute eines Objekts in der
/// Reihenfolge des Modells. Wenn wir ein bisher fehlendes Attribut ergaenzen (typisch
/// Baulicher_Zustand oder Bemerkung), muss es an der richtigen Stelle stehen. Die Reihenfolge
/// lesen wir darum aus den echten Objekten derselben Datei, statt sie zu raten oder das
/// ili-Modell nachzubauen.
/// </summary>
public sealed class Sia405AttributReihenfolge
{
    private readonly Dictionary<string, List<string>> _reihenfolgen = new(StringComparer.Ordinal);

    /// <summary>Nimmt die Elementnamen eines gelesenen Objekts in die bekannte Reihenfolge auf.</summary>
    public void Beobachte(string klasse, IReadOnlyList<string> elementNamen)
    {
        ArgumentNullException.ThrowIfNull(klasse);
        ArgumentNullException.ThrowIfNull(elementNamen);

        if (!_reihenfolgen.TryGetValue(klasse, out var bekannt))
        {
            bekannt = new List<string>();
            _reihenfolgen[klasse] = bekannt;
        }

        // Unbekannte Namen direkt hinter ihren zuletzt gesehenen bekannten Vorgaenger einfuegen.
        // So entsteht aus vielen unvollstaendigen Objekten eine stimmige Gesamtreihenfolge.
        var letztePosition = -1;
        foreach (var name in elementNamen)
        {
            if (string.IsNullOrEmpty(name))
                continue;

            var position = bekannt.IndexOf(name);
            if (position >= 0)
            {
                letztePosition = position;
                continue;
            }

            var einfuegen = letztePosition + 1;
            bekannt.Insert(einfuegen, name);
            letztePosition = einfuegen;
        }
    }

    /// <summary>Bekannte Reihenfolge einer Klasse; leer, wenn nichts beobachtet wurde.</summary>
    public IReadOnlyList<string> Fuer(string klasse)
        => _reihenfolgen.TryGetValue(klasse, out var bekannt)
            ? bekannt
            : Array.Empty<string>();

    /// <summary>
    /// Position, an der ein neues Element in ein Objekt eingefuegt wird: direkt hinter dem
    /// letzten vorhandenen Element, das im Modell vor dem neuen steht. Ohne bekannte
    /// Reihenfolge wird ans Ende angehaengt.
    /// </summary>
    public int IndexFuerEinfuegen(string klasse, IReadOnlyList<string> vorhandeneElemente, string neuesElement)
    {
        ArgumentNullException.ThrowIfNull(vorhandeneElemente);

        if (!_reihenfolgen.TryGetValue(klasse, out var bekannt))
            return vorhandeneElemente.Count;

        var zielPosition = bekannt.IndexOf(neuesElement);
        if (zielPosition < 0)
            return vorhandeneElemente.Count;

        var einfuegen = 0;
        for (var i = 0; i < vorhandeneElemente.Count; i++)
        {
            var position = bekannt.IndexOf(vorhandeneElemente[i]);
            if (position >= 0 && position < zielPosition)
                einfuegen = i + 1;
        }

        return einfuegen;
    }
}
