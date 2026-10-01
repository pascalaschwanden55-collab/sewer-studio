using System;
using System.Linq;

namespace AuswertungPro.Next.Application.Dossiers.Lookup;

/// <summary>
/// Baut den Dossiernamen in der bisher von Hand verwendeten Schreibweise:
/// "Liegenschaft Nr. 439 Beispiel". Der Name wird auch zum Ordnernamen, deshalb
/// werden Zeichen ersetzt, die in keinen Ordnernamen gehoeren.
/// </summary>
public static class DossierNameBuilder
{
    private static readonly char[] VerboteneZeichen =
        { '/', '\\', ':', '*', '?', '"', '<', '>', '|' };

    public static string Build(string parcelNumber, string? ownerName)
    {
        var nummer = (parcelNumber ?? string.Empty).Trim();
        var basis = $"Liegenschaft Nr. {nummer}";

        if (string.IsNullOrWhiteSpace(ownerName))
            return Saeubern(basis);

        // Der letzte Wortteil ist der Nachname; eine einteilige Firma bleibt ganz.
        var teile = ownerName.Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var kurz = teile.Length == 0 ? string.Empty : teile[^1];

        return Saeubern(kurz.Length == 0 ? basis : basis + " " + kurz);
    }

    private static string Saeubern(string wert)
    {
        var sauber = wert;
        foreach (var zeichen in VerboteneZeichen)
            sauber = sauber.Replace(zeichen, '-');

        return sauber.Trim();
    }
}
