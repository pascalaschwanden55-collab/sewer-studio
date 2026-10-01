using System;
using System.Threading;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Video;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Player;

/// <summary>
/// Verbindet die QGIS-Bruecke mit der laufenden Videowiedergabe — in beide Richtungen.
///
/// Hinweg: Die Bruecke FRAGT den Player nach Haltung, Videozeit, Dauer und den
/// codierten Stuetzstellen. Rein lesend; die Wiedergabe wird nicht angehalten und
/// dem Player wird nichts gemeldet. Bewusst als Abfrage (Pull) statt als Meldung
/// (Push): Die Bruecke fragt in eigenem Takt, ohne dass der Player je Bild etwas
/// anstossen muss.
///
/// Rueckweg: Ein Klick in der QGIS-Karte laesst das offene Video an diese Stelle
/// springen. Der Sprung ist die einzige Wirkung.
///
/// Die Logik liegt bewusst NICHT im <see cref="PlayerWindow"/>: Das Fenster ist
/// bereits eine sehr grosse Klasse (Waechter <c>MaintainabilityFitnessTests</c>),
/// und der Faden-Wechsel auf die Oberflaeche gehoert ohnehin in einen Controller.
/// </summary>
internal static class QgisVideoPositionController
{
    /// <summary>Wartezeit auf den Oberflaechenfaden. Haengt die App, antwortet die Bruecke trotzdem.</summary>
    private static readonly TimeSpan Geduld = TimeSpan.FromSeconds(2);

    /// <summary>Aktueller Zustand oder <c>null</c>. Wirft nie.</summary>
    public static VideoPositionEingang? Lies()
    {
        try
        {
            return PlayerWindow.TryBuildVideoPosition();
        }
        catch
        {
            // Die Bruecke darf den Player nie stoeren: ein Fehler heisst hier
            // schlicht "keine Position", nicht "Programmfehler".
            return null;
        }
    }

    /// <summary>
    /// Baut den Zustand aus den Bausteinen des offenen Players. Wird vom Fenster
    /// mit seinen eigenen Feldern gerufen — es reicht sie nur herein.
    /// </summary>
    public static VideoPositionEingang? Baue(
        PlayerPlaybackController? wiedergabe,
        PlayerWindowProtocolContext? protokoll)
    {
        if (wiedergabe is null)
            return null;
        if (!wiedergabe.TryGetPlaybackState(out var zeit, out var dauer, out var laeuft))
            return null;

        var record = protokoll?.HaltungRecord;
        var haltung = record?.GetFieldValue(FieldKeys.HoldingName);
        if (string.IsNullOrWhiteSpace(haltung))
            return null;

        return new VideoPositionEingang(
            haltung,
            zeit,
            dauer,
            laeuft,
            Haltungslaenge(record),
            VideoMeterStuetzstellen.AusProtokoll(record?.Protocol));
    }

    /// <summary>
    /// Rueckweg aus QGIS. Zustand lesen und Sprung setzen gehoeren beide auf den
    /// Oberflaechenfaden; die Bruecke ruft aus einem Hintergrundfaden.
    /// </summary>
    public static VideoSprungGrund Springe(VideoSprungAuftrag? auftrag)
    {
        if (auftrag is null)
            return VideoSprungGrund.KeinAuftrag;

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
            return VideoSprungGrund.KeinVideo;

        try
        {
            return dispatcher.Invoke(
                () => SpringeAufOberflaeche(auftrag),
                DispatcherPriority.Normal,
                CancellationToken.None,
                Geduld);
        }
        catch
        {
            // Auch eine Zeitueberschreitung heisst nur "nicht gesprungen".
            return VideoSprungGrund.SprungFehlgeschlagen;
        }
    }

    private static VideoSprungGrund SpringeAufOberflaeche(VideoSprungAuftrag auftrag)
    {
        var ergebnis = VideoSprungRechnung.Plane(PlayerWindow.TryBuildVideoPosition(), auftrag);
        if (!ergebnis.Bereit)
            return ergebnis.Grund;

        return PlayerWindow.TrySeekTo(ergebnis.Zeit!.Value)
            ? VideoSprungGrund.Bereit
            : VideoSprungGrund.SprungFehlgeschlagen;
    }

    /// <summary>Katasterlaenge in Metern; nur ein positiver Wert taugt als Bezug.</summary>
    private static double? Haltungslaenge(HaltungRecord? record)
    {
        var roh = record?.GetFieldValue(FieldKeys.HoldingLengthMeters);
        if (!FachzahlParser.TryParseMeasurement(roh, out var wert) || wert <= 0m)
            return null;

        return (double)wert;
    }
}
