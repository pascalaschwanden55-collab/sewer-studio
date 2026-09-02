using System.IO;

namespace AuswertungPro.Next.Infrastructure.Backup;

/// <summary>
/// Kennzeichnet eine verletzte Sicherheitsgrenze des Sicherungsziels: ein Pfad
/// ausserhalb des Spiegel-Roots oder eine Verknuepfung im Zielpfad.
///
/// Warum eine Markierung und keine eigene Ausnahmeklasse: Beim Spiegeln trugen
/// zwei voellig verschiedene Fehlschlaege denselben Ausnahmetyp — die harmlose,
/// nicht lesbare Quelldatei und der gefaehrliche Zielpfad, der aus dem Spiegel
/// herausfuehrt. Nur der zweite darf die Sicherung abbrechen.
///
/// <see cref="InvalidDataException"/> ist versiegelt, laesst sich also nicht
/// ableiten. Ein voellig neuer Typ wuerde dagegen aus den bestehenden
/// catch-Klauseln herausfallen und damit die Fehlerbehandlung an Stellen
/// veraendern, die mit dieser Unterscheidung nichts zu tun haben. Die Markierung
/// laesst Typ und Ablauf exakt wie bisher und macht nur den Grund unterscheidbar.
/// </summary>
internal static class BackupTargetBoundary
{
    private const string MarkerKey = "SewerStudio.BackupTargetBoundary";

    /// <summary>Erzeugt die markierte Ausnahme fuer eine verletzte Zielgrenze.</summary>
    public static InvalidDataException Fail(string message)
    {
        var exception = new InvalidDataException(message);
        exception.Data[MarkerKey] = true;
        return exception;
    }

    /// <summary>Erzeugt die markierte Ausnahme mit Ursache.</summary>
    public static InvalidDataException Fail(string message, Exception innerException)
    {
        var exception = new InvalidDataException(message, innerException);
        exception.Data[MarkerKey] = true;
        return exception;
    }

    /// <summary>Meldet, ob dieser Fehlschlag eine verletzte Zielgrenze ist.</summary>
    public static bool Marks(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        try
        {
            return exception.Data.Contains(MarkerKey);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            // Eine Ausnahme ohne benutzbare Data-Ablage gilt als nicht markiert:
            // im Zweifel Warnung statt stiller Abbruch waere falsch herum, deshalb
            // ist die sichere Richtung hier "kein Grenzfall" — der Aufrufer
            // behandelt sie dann wie jeden anderen Dateifehler.
            return false;
        }
    }
}
