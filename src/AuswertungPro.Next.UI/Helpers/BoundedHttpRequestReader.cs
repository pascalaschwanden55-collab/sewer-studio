using System.IO;
using System.Text;

namespace AuswertungPro.Next.UI.Helpers;

/// <summary>
/// Liest Anfragezeile und Kopfzeilen der kleinen lokalen HTTP-Server mit festen Grenzen.
///
/// Beide Server lasen zuvor mit <c>ReadLineAsync</c> ohne Laengengrenze. Die Anmeldung
/// wird erst nach dem vollstaendigen Einlesen geprueft — ein boesartiger oder defekter
/// Prozess desselben Windows-Benutzers konnte also schon vor der Anmeldung beliebig
/// grosse Kopfzeilen schicken und Speicher binden. Kein Fernangriff, die Server lauschen
/// nur auf Loopback, aber eine unnoetige offene Flanke.
///
/// Wird eine Grenze ueberschritten, liefert der Leser <c>null</c>. Der Aufrufer bricht die
/// Anfrage dann genauso ab wie bei einer unverstaendlichen Anfrage — es wird nichts
/// gekuerzt und nichts geraten.
/// </summary>
internal sealed class BoundedHttpRequestReader(TextReader reader)
{
    /// <summary>Zeichen der Anfragezeile ("GET /pfad?abfrage HTTP/1.1").</summary>
    public const int MaxRequestLineChars = 8 * 1024;

    /// <summary>Zeichen einer einzelnen Kopfzeile.</summary>
    public const int MaxHeaderLineChars = 8 * 1024;

    /// <summary>Anzahl Kopfzeilen.</summary>
    public const int MaxHeaderCount = 64;

    /// <summary>Zeichen aller Kopfzeilen zusammen.</summary>
    public const int MaxHeaderTotalChars = 32 * 1024;

    private readonly TextReader _reader = reader;

    /// <summary>Die Anfragezeile, oder <c>null</c> bei Dateiende, leerer Zeile oder Ueberlaenge.</summary>
    public async Task<string?> ReadRequestLineAsync(CancellationToken cancellationToken)
    {
        var zeile = await ReadLineAsync(MaxRequestLineChars, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(zeile) ? null : zeile;
    }

    /// <summary>
    /// Alle Kopfzeilen bis zur Leerzeile, oder <c>null</c>, wenn eine Grenze ueberschritten
    /// wurde. Eine leere Liste ist gueltig: eine Anfrage ganz ohne Kopfzeilen.
    /// </summary>
    public async Task<IReadOnlyList<string>?> ReadHeaderLinesAsync(CancellationToken cancellationToken)
    {
        var zeilen = new List<string>();
        var gesamt = 0;

        while (true)
        {
            var zeile = await ReadLineAsync(MaxHeaderLineChars, cancellationToken).ConfigureAwait(false);
            if (zeile is null)
                return null; // Ueberlaenge oder Verbindung vor der Leerzeile beendet.

            if (zeile.Length == 0)
                return zeilen; // Leerzeile: Ende des Kopfteils.

            if (zeilen.Count >= MaxHeaderCount)
                return null;

            gesamt += zeile.Length;
            if (gesamt > MaxHeaderTotalChars)
                return null;

            zeilen.Add(zeile);
        }
    }

    /// <summary>
    /// Eine Zeile bis <c>\n</c>, hoechstens <paramref name="maxChars"/> Zeichen.
    /// <c>null</c> bei Ueberlaenge oder Dateiende ohne Zeilenende.
    /// </summary>
    private async Task<string?> ReadLineAsync(int maxChars, CancellationToken cancellationToken)
    {
        var puffer = new char[1];
        var zeile = new StringBuilder();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var gelesen = await _reader.ReadAsync(puffer.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
            // Dateiende ohne Zeilenende: unvollstaendige Anfrage, nichts halb Gelesenes verwenden.
            if (gelesen == 0)
                return null;

            var zeichen = puffer[0];
            if (zeichen == '\n')
                return zeile.ToString().TrimEnd('\r');

            if (zeile.Length >= maxChars)
                return null;

            zeile.Append(zeichen);
        }
    }
}
