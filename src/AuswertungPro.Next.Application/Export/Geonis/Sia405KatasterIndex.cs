namespace AuswertungPro.Next.Application.Export.Geonis;

/// <summary>Ein Modelleintrag aus der HEADERSECTION der Kataster-Quelldatei.</summary>
public sealed record Sia405ModellReferenz(string Name, string Version, string Uri);

/// <summary>
/// Transfer-Kopfangaben, die die Ausgabedatei uebernimmt.
///
/// Wichtig: Wir erfinden kein Modell, sondern schreiben in demselben Modell, aus dem wir die
/// Identitaet (OBJ_ID) gelesen haben. Damit passt die Ausgabe immer zur Quelle.
/// </summary>
public sealed record Sia405ModellAngaben(
    string TransferNamespace,
    string HeaderVersion,
    string TopicPrefix,
    string? BasketId,
    IReadOnlyList<Sia405ModellReferenz> Modelle);

/// <summary>Haltung aus dem Kataster, reduziert auf das, was der Abgleich braucht.</summary>
public sealed record Sia405KatasterHaltung(
    string Bezeichnung,
    string Tid,
    string? ObjId,
    string? KanalTid,
    string? RohrprofilTid,
    string? LichteHoehe,
    string? LichteBreite,
    string? Material);

/// <summary>Kanal (Abwasserbauwerk der Haltung) — traegt Baulicher_Zustand und Bemerkung.</summary>
public sealed record Sia405KatasterKanal(
    string Tid,
    string? ObjId,
    string? Bezeichnung,
    string? BaulicherZustand,
    string? Bemerkung);

/// <summary>Normschacht aus dem Kataster.</summary>
public sealed record Sia405KatasterSchacht(
    string Bezeichnung,
    string Tid,
    string? ObjId,
    string? Dimension1,
    string? Dimension2,
    string? BaulicherZustand,
    string? Bemerkung);

/// <summary>Rohrprofil aus dem Kataster (liefert das Hoehen-Breiten-Verhaeltnis).</summary>
public sealed record Sia405KatasterRohrprofil(
    string Tid,
    string? ObjId,
    string? Bezeichnung,
    string? Profiltyp,
    string? HoehenBreitenverhaeltnis);

/// <summary>
/// Leseergebnis der Kataster-XTF: Identitaet und Ist-Werte der Objekte, die der Rueckschrieb
/// beruehren kann. Der Index ist die einzige Quelle fuer OBJ_ID und Modellangaben.
/// </summary>
public sealed class Sia405KatasterIndex
{
    public Sia405ModellAngaben Modell { get; init; } =
        new("http://www.interlis.ch/INTERLIS2.3", "2.3", string.Empty, null, Array.Empty<Sia405ModellReferenz>());

    /// <summary>Eindeutige Haltungen, Schluessel ist <see cref="Sia405NameKey"/>.</summary>
    public IReadOnlyDictionary<string, Sia405KatasterHaltung> Haltungen { get; init; } =
        new Dictionary<string, Sia405KatasterHaltung>(StringComparer.Ordinal);

    /// <summary>Bezeichnungen, die im Kataster mehrfach vorkommen — fuer diese gibt es keinen Abgleich.</summary>
    public IReadOnlySet<string> MehrdeutigeHaltungen { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, Sia405KatasterSchacht> Schaechte { get; init; } =
        new Dictionary<string, Sia405KatasterSchacht>(StringComparer.Ordinal);

    public IReadOnlySet<string> MehrdeutigeSchaechte { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, Sia405KatasterKanal> KanaeleNachTid { get; init; } =
        new Dictionary<string, Sia405KatasterKanal>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, Sia405KatasterRohrprofil> RohrprofileNachTid { get; init; } =
        new Dictionary<string, Sia405KatasterRohrprofil>(StringComparer.Ordinal);

    /// <summary>
    /// Materialschreibweisen der Quelldatei: Programmwert (gross) -> Originalschreibweise.
    /// Wir schreiben nur Werte zurueck, die im Kataster wirklich vorkommen.
    /// </summary>
    public IReadOnlyDictionary<string, string> MaterialVokabular { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Beobachtete Werte von Baulicher_Zustand (z. B. Z0..Z4).</summary>
    public IReadOnlySet<string> ZustandVokabular { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Erster gelesener Wert von Letzte_Aenderung. INTERLIS 2.3 kennt zwei Datumsschreibweisen
    /// (DATE = yyyymmdd, XMLDate = yyyy-mm-dd). Wir schreiben in derselben Schreibweise zurueck,
    /// die die Quelldatei verwendet, statt eine zu raten.
    /// </summary>
    public string? LetzteAenderungBeispiel { get; init; }

    public Sia405AttributReihenfolge AttributReihenfolge { get; init; } = new();
}
