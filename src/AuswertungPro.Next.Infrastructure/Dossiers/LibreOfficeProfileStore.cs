using System;
using System.IO;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Das Benutzerprofil, mit dem LibreOffice die Dossier-Umwandlung fährt.
///
/// Bisher bekam jede Umwandlung ein frisches Profil. Gemessen auf dieser
/// Maschine: 2,35 s je Lauf. Mit EINEM wiederverwendeten Profil sind es ab dem
/// zweiten Lauf rund 1,0 s — der grösste Teil der Zeit ging in den Aufbau des
/// Profils, nicht in die Umwandlung. Bei einer Vorschau, die nach jeder
/// Schreibpause neu rechnet, ist das der Unterschied zwischen Warten und
/// Arbeiten.
///
/// Eigen bleibt das Profil trotzdem: Es liegt im Temp-Ordner und nicht beim
/// Benutzer. Ein gleichzeitig geöffnetes LibreOffice wird dadurch nicht
/// gestört — das war der Grund für die Trennung und bleibt es.
/// </summary>
internal static class LibreOfficeProfileStore
{
    private static readonly object Schloss = new();
    private static string? _ordner;

    /// <summary>Der Profilordner dieses Programmlaufs.</summary>
    public static string Ordner()
    {
        lock (Schloss)
        {
            return _ordner ??= Neu();
        }
    }

    /// <summary>
    /// Verwirft das Profil. Nur nach einem Fehlschlag: Ein beschaedigtes Profil
    /// wuerde sonst jede weitere Umwandlung dauerhaft kosten.
    /// </summary>
    public static void Erneuere()
    {
        lock (Schloss)
        {
            var alt = _ordner;
            _ordner = Neu();
            Entferne(alt);
        }
    }

    private static string Neu()
        => Path.Combine(
            Path.GetTempPath(),
            "SewerStudio_LibreOffice_Profil_" + Guid.NewGuid().ToString("N"));

    private static void Entferne(string? ordner)
    {
        if (string.IsNullOrWhiteSpace(ordner))
            return;

        try
        {
            if (Directory.Exists(ordner))
                Directory.Delete(ordner, recursive: true);
        }
        catch
        {
            // Ein liegengebliebener Temp-Ordner ist kein Grund, die naechste
            // Umwandlung zu verhindern.
        }
    }
}
