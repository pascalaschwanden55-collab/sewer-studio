using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;
using System.Collections.ObjectModel;
using System.Linq;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DropdownOptionListTests
{
    [Fact]
    public void AddIfMissing_inserts_trimmed_value_at_top()
    {
        var options = new ObservableCollection<string> { "Nein" };

        var added = DropdownOptionList.AddIfMissing(options, "  Ja  ");

        Assert.True(added);
        Assert.Equal(new[] { "Ja", "Nein" }, options);
    }

    [Fact]
    public void AddIfMissing_ignores_empty_and_case_duplicates()
    {
        var options = new ObservableCollection<string> { "Ja" };

        Assert.False(DropdownOptionList.AddIfMissing(options, "  "));
        Assert.False(DropdownOptionList.AddIfMissing(options, "ja"));

        Assert.Equal(new[] { "Ja" }, options);
    }

    [Fact]
    public void Remove_deletes_case_insensitive_match()
    {
        var options = new ObservableCollection<string> { "Ja", "Nein" };

        var removed = DropdownOptionList.Remove(options, "nein");

        Assert.True(removed);
        Assert.Equal(new[] { "Ja" }, options);
    }

    [Fact]
    public void EnsureExact_replaces_missing_or_reordered_options()
    {
        var options = new ObservableCollection<string> { "Privat", "Kanton" };

        var changed = DropdownOptionList.EnsureExact(options, new[] { "Kanton", "Privat" });

        Assert.True(changed);
        Assert.Equal(new[] { "Kanton", "Privat" }, options);
    }

    [Fact]
    public void EnsureExact_returns_false_when_already_equal()
    {
        var options = new ObservableCollection<string> { "Kanton", "Privat" };

        var changed = DropdownOptionList.EnsureExact(options, new[] { "Kanton", "Privat" });

        Assert.False(changed);
        Assert.Equal(new[] { "Kanton", "Privat" }, options);
    }

    [Fact]
    public void Shared_eigentuemer_defaults_are_single_source()
    {
        var options = new ObservableCollection<string> { "Privat" };

        DropdownOptionList.EnsureExact(options, DropdownOptionsStore.FixedEigentuemerOptions);

        // Die sechs Sammelbegriffe stehen weiterhin vorn und in dieser Reihenfolge.
        // Altprojekte fuehren sie, und beide Excel-Vorlagen faerben genau sie.
        // "AWU" und "Kanton" stehen nicht zur Auswahl, bleiben dort aber gueltig.
        Assert.Equal(
            new[] { "Privat", "Abwasser Uri", "Gemeinde", "Kanton Uri", "Bund", "unbekannt" },
            options.Take(6));

        // Dahinter die 27 Werte, die im Abwassernetz des Kantons wirklich stehen.
        // Ohne sie schreibt der XTF-Export nur "Gemeinde" statt der Gemeinde, der
        // die Leitung gehoert.
        //
        // GEMESSEN am 2026-09-02, nicht angenommen: `org_eigentuemer` in den lokalen
        // QGIS-Kopien "Leitungen Lokal.gpkg" (110'297 Leitungen) und
        // "Schaechte-Selektioniert.gpkg" (68'735 Schaechte). Beide Layer fuehren
        // exakt dieselben 27 Werte — kein Wert kommt nur auf einer Seite vor. Dazu
        // je ein leeres Feld (426 Leitungen, 4521 Schaechte).
        //
        // Die Liste ist damit zeichengenau der Bestand: Umlaute und der Kantonszusatz
        // "(UR)" gehen so in die XTF, wie sie im Kataster stehen.
        Assert.Contains("ASTRA - Bundesamt für Strassen", options);   // 14'497 Leitungen
        Assert.Contains("Korporation Uri", options);                   //    908
        Assert.Contains("Meliorationsgenossenschaft Reussebene Uri", options); // 1'041
        Assert.Contains("Meliorationsgesellschaft Seedorf", options);  //    608

        // Alle 19 Urner Gemeinden, zusammen 2'722 Leitungen. Genau drei tragen den
        // Kantonszusatz — Altdorf, Buerglen und Seedorf gibt es auch in anderen
        // Kantonen. Die uebrigen 16 stehen ohne; auch das ist gemessen.
        var gemeinden = new[]
        {
            "Altdorf (UR)", "Andermatt", "Attinghausen", "Bürglen (UR)", "Erstfeld",
            "Flüelen", "Göschenen", "Gurtnellen", "Hospental", "Isenthal", "Realp",
            "Schattdorf", "Seedorf (UR)", "Seelisberg", "Silenen", "Sisikon",
            "Spiringen", "Unterschächen", "Wassen"
        };
        Assert.Equal(19, gemeinden.Length);
        Assert.Equal(3, gemeinden.Count(g => g.EndsWith(" (UR)", StringComparison.Ordinal)));
        foreach (var gemeinde in gemeinden)
            Assert.Contains(gemeinde, options);

        // Ein doppelter Eintrag waere in der Auswahl zweimal sichtbar.
        Assert.Equal(options.Count, options.Distinct(StringComparer.Ordinal).Count());
    }

    // Jeder anwaehlbare Eigentuemer muss auch einen Organisationstyp haben — sonst
    // waehlt der Mensch einen Wert, den der XTF-Export danach still liegen laesst.
    [Fact]
    public void Jeder_anwaehlbare_Eigentuemer_kann_auch_exportiert_werden()
    {
        var ohneTyp = DropdownOptionsStore.FixedEigentuemerOptions
            .Where(wert => wert.Length > 0)
            .Where(wert => EigentumVokabular.NachOrganisationstyp(wert) is null)
            .ToArray();

        Assert.True(ohneTyp.Length == 0, "Ohne Organisationstyp: " + string.Join(", ", ohneTyp));
    }

    [Fact]
    public void ExtractText_handles_null_string_and_objects()
    {
        Assert.Equal("", DropdownOptionList.ExtractText(null));
        Assert.Equal("Text", DropdownOptionList.ExtractText("Text"));
        Assert.Equal("42", DropdownOptionList.ExtractText(42));
    }
}
