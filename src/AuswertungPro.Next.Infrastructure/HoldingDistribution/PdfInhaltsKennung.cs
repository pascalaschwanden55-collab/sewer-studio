using System.Security.Cryptography;
using System.Text;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Ersetzt die zufaellige Dokumentkennung (<c>/ID</c> im Trailer), die der PDF-Baukasten bei jedem
/// Schreiben neu vergibt, durch eine aus dem Inhalt berechnete. Sonst ist ein neu erzeugter
/// Seitenauszug nie bytegleich mit dem aus einem frueheren Lauf, und die Verteilung legt bei jeder
/// Wiederholung eine weitere Kopie an. Die Kennung behaelt ihre Laenge; Querverweise bleiben gueltig.
/// </summary>
internal static class PdfInhaltsKennung
{
    private static readonly byte[] IdMarke = Encoding.ASCII.GetBytes("/ID [");

    internal static byte[] Festlegen(byte[] pdf)
    {
        var start = LetzteFundstelle(pdf, IdMarke);
        if (start < 0)
            return pdf;

        // Erwartet: /ID [ <32 hex><32 hex>]  (Leerzeichen zwischen den Teilen erlaubt)
        var positionen = new List<int>(2);
        var i = start + IdMarke.Length;
        while (positionen.Count < 2 && i < pdf.Length)
        {
            if (pdf[i] == (byte)'<')
            {
                if (i + 33 >= pdf.Length || pdf[i + 33] != (byte)'>' || !IstHex(pdf, i + 1, 32))
                    return pdf;
                positionen.Add(i + 1);
                i += 34;
            }
            else if (pdf[i] == (byte)' ')
            {
                i++;
            }
            else
            {
                return pdf;
            }
        }
        if (positionen.Count != 2)
            return pdf;

        var ergebnis = (byte[])pdf.Clone();
        foreach (var p in positionen)
            Array.Fill(ergebnis, (byte)'0', p, 32);

        var kennung = Encoding.ASCII.GetBytes(Convert.ToHexString(SHA256.HashData(ergebnis))[..32]);
        foreach (var p in positionen)
            Buffer.BlockCopy(kennung, 0, ergebnis, p, 32);
        return ergebnis;
    }

    private static bool IstHex(byte[] daten, int ab, int laenge)
    {
        for (var k = ab; k < ab + laenge; k++)
        {
            var c = (char)daten[k];
            if (!Uri.IsHexDigit(c))
                return false;
        }
        return true;
    }

    private static int LetzteFundstelle(byte[] daten, byte[] muster)
    {
        for (var i = daten.Length - muster.Length; i >= 0; i--)
        {
            var gleich = true;
            for (var k = 0; k < muster.Length && gleich; k++)
                gleich = daten[i + k] == muster[k];
            if (gleich)
                return i;
        }
        return -1;
    }
}
