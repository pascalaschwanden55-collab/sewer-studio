using System;
using System.Collections.Generic;
using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Was importiert oder von Hand eingetragen wurde, darf der Nachschlag nicht
/// veraendern. Zwei Sperren sichern das:
///
/// 1. Der Menuepunkt erscheint nur an einem leeren Feld
///    (<c>RecordDetailItem.KannNachschlagen</c>).
/// 2. Unmittelbar vor dem Schreiben wird erneut geprueft, ob das Feld noch
///    leer ist. Ohne diese zweite Pruefung koennte ein Wert ueberschrieben
///    werden, der zwischen Menueaufruf und Bestaetigung entstanden ist —
///    geschrieben wird mit userEdited: true, das den Handwert-Schutz des
///    Datensatzes bewusst umgeht.
/// </summary>
public sealed class NachschlagUeberschreibtNichtsTests
{
    private static string Quelle()
        => File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Pages", "SchaechtePage.Nachschlag.cs"));

    [Fact]
    public void Vor_dem_Schreiben_wird_erneut_auf_leer_geprueft()
    {
        var uebernahme = MethodeLesen(Quelle(), "UebernimmNachschlag");

        Assert.Contains("IsNullOrWhiteSpace", uebernahme, StringComparison.Ordinal);
        Assert.Contains("GetFieldValue", uebernahme, StringComparison.Ordinal);
    }

    [Fact]
    public void Geschrieben_wird_mit_der_Herkunft_des_Nachschlags()
    {
        var uebernahme = MethodeLesen(Quelle(), "UebernimmNachschlag");

        Assert.Contains("FieldSource.Kataster", uebernahme, StringComparison.Ordinal);
        Assert.Contains("FieldSource.Grundbuch", uebernahme, StringComparison.Ordinal);
        Assert.Contains("userEdited: true", uebernahme, StringComparison.Ordinal);
    }

    /// <summary>
    /// Liest genau den Rumpf einer Methode. Zwei Fallen sind dabei umgangen:
    /// Ein Vergleich ueber die ganze Datei waere blind, weil die gesuchten
    /// Zeichenfolgen dort auch anderswo stehen — und die Suche muss die
    /// Definition treffen, nicht den frueher stehenden Aufruf.
    /// </summary>
    private static string MethodeLesen(string quelle, string name)
    {
        var start = quelle.IndexOf("private void " + name + "(", StringComparison.Ordinal);
        Assert.True(start >= 0, "Methode " + name + " nicht gefunden.");

        // Bis zur schliessenden Klammer der Methode: eine Zeile, die genau aus
        // vier Leerzeichen und der Klammer besteht. Ein groberer Schnitt wuerde
        // Folgemethoden mitlesen und den Waechter blind machen.
        var zeilen = quelle[start..].Split('\n');
        var rumpf = new List<string>();

        foreach (var zeile in zeilen)
        {
            rumpf.Add(zeile);
            if (rumpf.Count > 1 && zeile.TrimEnd('\r') == "    }")
                break;
        }

        Assert.True(rumpf.Count > 2, "Rumpf von " + name + " nicht abgegrenzt.");
        return string.Join('\n', rumpf);
    }
}
