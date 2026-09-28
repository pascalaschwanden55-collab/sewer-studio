using System;
using System.IO;
using System.Linq;
using System.Text;
using AuswertungPro.Next.Application.WebGis;

namespace AuswertungPro.Next.Infrastructure.WebGis;

/// <summary>
/// Das Änderungslog EINES Schreiblaufs in &lt;Projekt&gt;\__WebGIS_Export\WebGIS_Log.txt. Anders als
/// <see cref="WebGisBerichtAblage.HaengeAnLog"/> verschluckt es keinen Fehler: Jede Zeile wird
/// angehängt und auf die Platte geschrieben, bevor der Aufruf zurückkehrt, und jede Zeile trägt
/// die Laufkennung — so lassen sich Start, Objekte, Massnahmen und Abschluss eines Laufs
/// zusammen finden, auch wenn zwei Läufe im selben Log stehen (Plan WG03, 28.09.2026).
/// </summary>
public sealed class WebGisLaufLog : IWebGisLaufLog
{
    private readonly string _ordner;

    public WebGisLaufLog(string ordner, string? laufId = null)
    {
        _ordner = ordner ?? throw new ArgumentNullException(nameof(ordner));
        LaufId = string.IsNullOrWhiteSpace(laufId)
            ? DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]
            : laufId.Trim();
    }

    public string LaufId { get; }

    public void Schreibe(string zeilen)
    {
        if (string.IsNullOrEmpty(zeilen)) return;
        var text = new StringBuilder();
        foreach (var zeile in zeilen.Replace("\r\n", "\n").Split('\n').Where(z => z.Length > 0))
            text.Append("Lauf ").Append(LaufId).Append(" | ").Append(zeile).Append(Environment.NewLine);

        try
        {
            var datei = WebGisBerichtAblage.SichereLogDatei(_ordner);
            using var strom = new FileStream(datei, FileMode.Append, FileAccess.Write, FileShare.Read);
            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text.ToString());
            strom.Write(bytes, 0, bytes.Length);
            strom.Flush(flushToDisk: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new WebGisLogException(
                "Das WebGIS-Änderungslog ist nicht schreibbar (" + ex.Message + ").", ex);
        }
    }

    public bool VersucheSchreibe(string zeilen)
    {
        try
        {
            Schreibe(zeilen);
            return true;
        }
        catch (WebGisLogException)
        {
            return false;
        }
    }
}
