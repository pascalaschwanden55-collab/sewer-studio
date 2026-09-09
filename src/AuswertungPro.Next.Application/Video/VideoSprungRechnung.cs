using System;

namespace AuswertungPro.Next.Application.Video;

/// <summary>Auftrag aus QGIS: in dieser Haltung an diesen Meter springen.</summary>
/// <param name="Haltung">Name der angeklickten Haltung.</param>
/// <param name="Meter">Stelle in der Haltung, ab Aufnahmestart gerechnet.</param>
public sealed record VideoSprungAuftrag(string Haltung, double Meter);

/// <summary>Warum ein Sprung ausgefuehrt wurde — oder eben nicht.</summary>
public enum VideoSprungGrund
{
    /// <summary>Zielzeit steht fest.</summary>
    Bereit,

    /// <summary>Es laeuft kein Video mit bekannter Haltung.</summary>
    KeinVideo,

    /// <summary>Der Auftrag nennt keine Haltung.</summary>
    KeinAuftrag,

    /// <summary>Der Klick galt einer anderen Haltung als der laufenden.</summary>
    FremdeHaltung,

    /// <summary>Der gemeldete Meterwert ist keine brauchbare Zahl.</summary>
    UngueltigerMeter,

    /// <summary>Weder Stuetzstellen noch Laenge und Dauer — die Zeit bleibt unbekannt.</summary>
    NichtBestimmbar,

    /// <summary>
    /// Die Zielzeit stand fest, der Player hat den Sprung aber nicht angenommen.
    /// Setzt nur die Ausfuehrung; <see cref="VideoSprungRechnung.Plane"/> nie.
    /// </summary>
    SprungFehlgeschlagen
}

/// <summary>Ergebnis der Planung. <see cref="Zeit"/> ist nur bei <see cref="Bereit"/> gesetzt.</summary>
public sealed record VideoSprungErgebnis(VideoSprungGrund Grund, TimeSpan? Zeit, VideoMeterQuelle Quelle)
{
    public bool Bereit => Grund == VideoSprungGrund.Bereit && Zeit is not null;
}

/// <summary>
/// Rueckweg der QGIS-Bruecke: aus einem angeklickten Meterwert wird eine Videozeit.
/// Es sind dieselben Stuetzstellen wie beim Hinweg, nur umgekehrt gelesen.
///
/// Der wichtigste Schutz steht am Anfang: Gesprungen wird ausschliesslich in der
/// Haltung, die gerade laeuft. Ein Klick auf eine Nachbarhaltung darf das offene
/// Video nicht verstellen — und die Gegenfahrt ist eine eigene Haltung mit eigener
/// Aufnahme, ihr Name wird deshalb nie umgedreht oder normalisiert.
///
/// Reine Rechnung ohne Oberflaeche, Datei- oder Netzzugriff. Sie fuehrt den Sprung
/// nicht aus, sondern sagt nur, wohin er ginge.
/// </summary>
public static class VideoSprungRechnung
{
    public static VideoSprungErgebnis Plane(VideoPositionEingang? eingang, VideoSprungAuftrag? auftrag)
    {
        if (eingang is null || string.IsNullOrWhiteSpace(eingang.Haltung))
            return Ohne(VideoSprungGrund.KeinVideo);

        if (auftrag is null || string.IsNullOrWhiteSpace(auftrag.Haltung))
            return Ohne(VideoSprungGrund.KeinAuftrag);

        if (!GleicheHaltung(eingang.Haltung, auftrag.Haltung))
            return Ohne(VideoSprungGrund.FremdeHaltung);

        if (double.IsNaN(auftrag.Meter) || double.IsInfinity(auftrag.Meter) || auftrag.Meter < 0.0)
            return Ohne(VideoSprungGrund.UngueltigerMeter);

        var spur = VideoMeterSpur.Baue(
            eingang.Stuetzstellen,
            eingang.HaltungslaengeM,
            eingang.Dauer);

        return spur.ZeitBei(auftrag.Meter) is { } zeit
            ? new VideoSprungErgebnis(VideoSprungGrund.Bereit, zeit, spur.Quelle)
            : new VideoSprungErgebnis(VideoSprungGrund.NichtBestimmbar, null, spur.Quelle);
    }

    /// <summary>
    /// Nur Leerzeichen und Gross-/Kleinschreibung werden uebergangen. Die Namensteile
    /// selbst bleiben unangetastet: "80475-80462" und "80462-80475" sind zwei Haltungen.
    /// </summary>
    private static bool GleicheHaltung(string laufend, string angeklickt)
        => string.Equals(laufend.Trim(), angeklickt.Trim(), StringComparison.OrdinalIgnoreCase);

    private static VideoSprungErgebnis Ohne(VideoSprungGrund grund)
        => new(grund, null, VideoMeterQuelle.Unbekannt);
}
