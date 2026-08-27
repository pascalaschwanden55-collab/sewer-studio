using System;
using System.Security.Cryptography;
using System.Text;

namespace AuswertungPro.Next.Application.Dossiers.Preview;

/// <summary>
/// Der Name der unsichtbaren Word-Textmarke, die eine fuellbare Stelle im
/// erzeugten Dokument kennzeichnet.
///
/// Warum es sie gibt: Eine PDF traegt keine Feldnamen. Die Vorschau erkennt ein
/// Feld deshalb bisher an seinem TEXT und verweigert bewusst jeden Treffer, wenn
/// mehrere Felder denselben Text tragen — sonst wuerde geraten. Seit fehlende
/// Angaben als „unbekannt" erscheinen, betrifft das viele Zellen zugleich.
///
/// Eine Textmarke wird beim Umwandeln zu einem benannten Ziel der PDF, mit
/// Seitennummer und exakter Position. Gemessen mit LibreOffice: auch fuer eine
/// voellig leere Zelle. Damit ist die Zuordnung exakt statt geraten.
///
/// Zwei harte Regeln stecken im Namen:
/// <list type="bullet">
/// <item>Nur Buchstaben und Ziffern. Aus <c>SSFELD_Beilagen</c> machte
/// LibreOffice <c>SSFELD5FBeilagen</c> — ein Unterstrich ueberlebt nicht.</item>
/// <item>Aus derselben fachlichen Adresse immer derselbe Name. Die Vorschau
/// baut den Namen aus dem Ziel neu und schlaegt ihn nach; sie muss ihn nie
/// zurueckuebersetzen.</item>
/// </list>
/// </summary>
public static class DossierPdfFieldMarker
{
    /// <summary>Vorsilbe aller eigenen Marken. Word legt eigene an (_Toc…, _GoBack).</summary>
    public const string Prefix = "SSFELD";

    /// <summary>
    /// Der Markenname einer fachlichen Adresse. Word erlaubt hoechstens
    /// 40 Zeichen. 136 Bit des SHA-256-Werts halten den Namen kurz und machen
    /// Kollisionen auch bei sehr vielen Dossierfeldern praktisch ausgeschlossen.
    /// </summary>
    public static string Name(DossierPreviewTarget target)
    {
        var addressBytes = Encoding.UTF8.GetBytes(Address(target));
        var hash = SHA256.HashData(addressBytes);
        return Prefix + Convert.ToHexString(hash.AsSpan(0, 17));
    }

    /// <summary>
    /// Wahr, wenn dieser Zielname von uns stammt. Fremde Marken der Vorlage
    /// duerfen nie als Feldziel gedeutet werden.
    /// </summary>
    public static bool IsMarker(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var wert = name.Trim();
        if (!wert.StartsWith(Prefix, StringComparison.Ordinal))
            return false;

        var rest = wert.AsSpan(Prefix.Length);
        if (rest.Length == 0 || rest.Length % 2 != 0)
            return false;

        foreach (var zeichen in rest)
        {
            if (!Uri.IsHexDigit(zeichen))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Die fachliche Adresse als Text. Der Trenner gehoert dazu: ohne ihn
    /// waeren „Themen|1|Text" und „Themen1|Text" derselbe Name.
    /// </summary>
    private static string Address(DossierPreviewTarget target) => target.Kind switch
    {
        DossierPreviewTargetKind.RowCell =>
            $"C|{target.Key}|{target.RowIndex}|{target.CellKey}",
        DossierPreviewTargetKind.Row => $"R|{target.Key}|{target.RowIndex}",
        DossierPreviewTargetKind.Literal => $"L|{target.Key}",
        _ => $"F|{target.Key}"
    };

}
