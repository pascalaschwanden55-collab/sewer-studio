using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.ProjektPruefung;

public enum ProjektPruefbereich { Dateien, KiBefunde, Meterangaben, Schachthoehen, Eingabefelder }

/// <summary>Stabile Sprungadresse; keine Live-Datensaetze aus einem Hintergrundlauf.</summary>
public sealed record ProjektPruefpunkt(ProjektPruefbereich Bereich, string Objektart, Guid ObjektId,
    string Objektname, string Meldung, Guid? EintragId = null, Guid? AkteId = null, string? FeldId = null,
    string? Speicherfeld = null)
{
    public string BereichText => Bereich switch
    {
        ProjektPruefbereich.Dateien => "Dateiverweise",
        ProjektPruefbereich.KiBefunde => "Offene KI-Befunde",
        ProjektPruefbereich.Meterangaben => "Meterangaben",
        ProjektPruefbereich.Schachthoehen => "Schachthöhen",
        _ => "Eingabefelder"
    };
    public string ObjektText => $"{(Objektart == "haltung" ? "Haltung" : "Schacht")} {Objektname}";
}

public sealed record ProjektPruefergebnis(IReadOnlyList<ProjektPruefpunkt> Punkte, int Haltungen, int Schaechte);

/// <summary>Prueft ausschliesslich einen losgeloesten Projektstand; schreibt keine Daten oder Dateien.</summary>
public interface IProjektPruefung
{
    ProjektPruefergebnis Pruefe(Project projekt, string? projektdatei, CancellationToken cancellationToken);
}
