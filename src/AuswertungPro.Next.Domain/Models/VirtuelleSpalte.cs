using System;

namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Nova-Fixwelle 2b, Runde 2: Virtuelle Tabellenspalten sind KEINE Felder.
///
/// Die vier Statusspalten der Nova-Tabellen (KI, Pruefung, Video, Protokoll) tragen nur
/// einen Schluessel zur Wiedererkennung in der Tabelle. Sie stehen nicht im
/// <see cref="FieldCatalog"/>, gehen in keinen Export und duerfen nie in
/// <c>Fields</c> eines Datensatzes landen — sonst entstuende aus einem Rechtsklick auf
/// ihren Kopf ein erfundener Feldname in jedem Datensatz und in jeder Projektdatei.
///
/// Genau das ist am 07.09.2026 im Schacht-Rechtsklickpfad passiert: „Spalte leeren" schrieb
/// <c>Nova_Protokoll</c> in JEDEN Schachtdatensatz. Die Oberflaeche hat den Fall inzwischen
/// abgefangen; diese Regel ist die zweite, tiefere Sperre direkt am Datensatz.
///
/// Bewusst eine <see cref="ArgumentException"/> und kein stilles Ignorieren: Ein
/// verschluckter Schreibversuch sieht fuer den Aufrufer wie ein Erfolg aus. Wer hierher
/// laeuft, hat einen Fehler im Programm — und der soll sichtbar sein.
/// </summary>
public static class VirtuelleSpalte
{
    /// <summary>Praefix aller virtuellen Spaltenschluessel. Ein echtes Feld traegt ihn nie.</summary>
    public const string Praefix = "Nova_";

    public static bool IstVirtuell(string? feld)
        => feld is not null && feld.StartsWith(Praefix, StringComparison.Ordinal);

    /// <summary>Weist einen virtuellen Schluessel ab, bevor er in <c>Fields</c> gelangt.</summary>
    public static void WeiseAb(string? feld, string parameterName)
    {
        if (IstVirtuell(feld))
        {
            throw new ArgumentException(
                $"\"{feld}\" ist eine virtuelle Tabellenspalte und kein Feld. Ein Wert darf dort "
                + "nie gespeichert werden.",
                parameterName);
        }
    }
}
