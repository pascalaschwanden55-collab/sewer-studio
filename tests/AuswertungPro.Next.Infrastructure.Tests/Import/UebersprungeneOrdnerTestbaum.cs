using AuswertungPro.Next.Infrastructure.Tests.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Testaufbau fuer R1 (Deepscan 02.10.2026): eine Quelle mit einem verknuepften
/// Unterordner, den die sichere Dateisuche nicht betritt. Das Ziel der Verknuepfung
/// liegt ausserhalb der Quelle und enthaelt Dateien, die deshalb fehlen.
/// Nur in Tests mit <see cref="JunctionFactAttribute"/> verwenden.
/// </summary>
internal sealed class UebersprungeneOrdnerTestbaum : IDisposable
{
    private readonly List<string> _verknuepfungen = new();

    public UebersprungeneOrdnerTestbaum(string praefix)
    {
        Wurzel = Path.Combine(Path.GetTempPath(), $"{praefix}-{Guid.NewGuid():N}");
        Quelle = Path.Combine(Wurzel, "quelle");
        Extern = Path.Combine(Wurzel, "extern");
        Directory.CreateDirectory(Quelle);
        Directory.CreateDirectory(Extern);
    }

    public string Wurzel { get; }

    public string Quelle { get; }

    /// <summary>Ordner ausserhalb der Quelle; sein Inhalt bleibt fuer die Suche unsichtbar.</summary>
    public string Extern { get; }

    /// <summary>Legt unter <paramref name="ordner"/> eine Verzeichnis-Verknuepfung auf <see cref="Extern"/> an.</summary>
    public string Verknuepfe(string ordner, string name = "verknuepft")
    {
        Directory.CreateDirectory(ordner);
        var link = Path.Combine(ordner, name);
        JunctionTestSupport.CreateDirectoryLink(link, Extern);
        _verknuepfungen.Add(link);
        return link;
    }

    /// <summary>Die erwartete Berichtszeile fuer eine Verknuepfung.</summary>
    public static string ErwarteteZeile(string link)
        => $"Ordner «{link}» übersprungen: Verknüpfung, wird nicht betreten";

    public void Dispose()
    {
        foreach (var link in _verknuepfungen)
        {
            try
            {
                if (Directory.Exists(link))
                    Directory.Delete(link);
            }
            catch
            {
                // Nur Test-Aufraeumen.
            }
        }

        try
        {
            if (Directory.Exists(Wurzel))
                Directory.Delete(Wurzel, recursive: true);
        }
        catch
        {
            // Nur Test-Aufraeumen.
        }
    }
}
