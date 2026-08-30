using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Lookup;

/// <summary>Welche Art von Bauteil nachgeschlagen wird.</summary>
public enum BauteilArt
{
    Schacht,
    Haltung
}

/// <summary>
/// Was nachgeschlagen werden soll. <paramref name="Bauteilnummer"/> ist die
/// Schachtnummer beziehungsweise der Haltungsname.
/// </summary>
public sealed record FeldNachschlagAnfrage(
    string Bauteilnummer,
    string Feldname,
    BauteilArt Art = BauteilArt.Schacht);

/// <summary>
/// Ein gefundener Wert samt seiner Herkunft. <paramref name="QuelleKlartext"/>
/// steht im Vorschlagsfenster, <paramref name="Herkunftshinweis"/> wird beim
/// Uebernehmen in die Feldmetadaten geschrieben.
/// </summary>
public sealed record FeldVorschlag(
    string Wert,
    string QuelleKlartext,
    string Herkunftshinweis);

/// <summary>
/// Das Ergebnis eines Nachschlags. Jeder Zustand ist eigenstaendig: Ein
/// technischer Fehler oder eine Drosselung darf nie wie "nicht gefunden"
/// aussehen, sonst haelt der Benutzer eine Stoerung fuer eine Datenluecke.
/// </summary>
public abstract record FeldNachschlagErgebnis
{
    public sealed record Gefunden(FeldVorschlag Vorschlag) : FeldNachschlagErgebnis;

    public sealed record Mehrdeutig(IReadOnlyList<FeldVorschlag> Kandidaten) : FeldNachschlagErgebnis;

    public sealed record NichtGefunden(string Grund) : FeldNachschlagErgebnis;

    public sealed record Gedrosselt : FeldNachschlagErgebnis;

    public sealed record Fehler(string Meldung) : FeldNachschlagErgebnis;
}

/// <summary>Eine Quelle, die einen Feldwert liefern kann.</summary>
public interface IFeldWertNachschlag
{
    Task<FeldNachschlagErgebnis> SucheAsync(
        FeldNachschlagAnfrage anfrage, CancellationToken ct = default);
}
