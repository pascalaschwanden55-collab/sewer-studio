using System.Linq;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers.Lookup;

public sealed class SearchChDirectoryClientTests
{
    [Fact]
    public async Task Ohne_Schluessel_wird_nichts_abgefragt()
    {
        // Die Nutzungsbedingungen erlauben nur die Schnittstelle mit eigenem
        // Schluessel. Ohne ihn darf das Programm NICHT auf die Webseite
        // ausweichen, sondern muss den Mangel melden.
        using var client = new SearchChDirectoryClient(() => null);

        Assert.False(client.IsConfigured);

        var ergebnis = await client.FindAsync("Muster", "Erstfeld");

        Assert.True(ergebnis.IsUnavailable);
        Assert.Empty(ergebnis.Entries);
        Assert.Contains("Schlüssel", ergebnis.Unavailable!, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Die_Quellenangabe_nennt_den_Rechteinhaber()
    {
        using var client = new SearchChDirectoryClient(() => "abc");

        Assert.True(client.IsConfigured);
        Assert.Contains("Swisscom Directories", client.Attribution, System.StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kein XML")]
    [InlineData("<html><body>Fehler</body></html>")]
    public void Eine_unerwartete_Antwort_ergibt_keinen_Eintrag(string? antwort)
    {
        // Lieber leer als eine geratene Nummer im Brief an den Eigentuemer.
        Assert.Empty(SearchChDirectoryClient.Parse(antwort));
    }

    [Fact]
    public void Liest_Name_Nummer_und_Adresse_aus_der_dokumentierten_Form()
    {
        // Nachgebaut nach der veroeffentlichten Form der Schnittstelle. Das
        // beweist den Leser, NICHT die echte Antwort — die braucht einen Lauf
        // mit richtigem Schluessel.
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <feed xmlns="http://www.w3.org/2005/Atom"
                  xmlns:tel="http://tel.search.ch/api/spec/result/1.0/">
              <entry>
                <title>Muster Martin</title>
                <tel:name>Muster</tel:name>
                <tel:firstname>Martin</tel:firstname>
                <tel:street>Musterweg</tel:street>
                <tel:streetno>3</tel:streetno>
                <tel:zip>6472</tel:zip>
                <tel:city>Musterdorf</tel:city>
                <tel:phone>041 111 22 33</tel:phone>
                <tel:email>martin@example.invalid</tel:email>
              </entry>
            </feed>
            """;

        var eintrag = Assert.Single(SearchChDirectoryClient.Parse(xml));

        Assert.Equal("Muster Martin", eintrag.Name);
        Assert.Equal("Musterweg 3", eintrag.Address);
        Assert.Equal("6472", eintrag.PostalCode);
        Assert.Equal("Musterdorf", eintrag.Town);
        Assert.Equal("041 111 22 33", eintrag.Phone);
        Assert.Equal("martin@example.invalid", eintrag.Mail);
    }

    [Fact]
    public void Ein_Eintrag_ohne_Nummer_und_ohne_Mail_traegt_nichts_bei()
    {
        var xml = """
            <feed xmlns="http://www.w3.org/2005/Atom"
                  xmlns:tel="http://tel.search.ch/api/spec/result/1.0/">
              <entry><tel:name>Muster</tel:name><tel:city>Musterdorf</tel:city></entry>
            </feed>
            """;

        Assert.Empty(SearchChDirectoryClient.Parse(xml));
    }

    [Fact]
    public void Mehrere_Treffer_ergeben_keinen_eindeutigen_Wert()
    {
        // Bei zwei gleichnamigen Personen im Ort bleibt das Feld leer.
        var xml = """
            <feed xmlns="http://www.w3.org/2005/Atom"
                  xmlns:tel="http://tel.search.ch/api/spec/result/1.0/">
              <entry><tel:name>Muster</tel:name><tel:phone>041 111 22 33</tel:phone></entry>
              <entry><tel:name>Muster</tel:name><tel:phone>041 444 55 66</tel:phone></entry>
            </feed>
            """;

        var ergebnis = new DirectoryLookupResult(SearchChDirectoryClient.Parse(xml));

        Assert.Equal(2, ergebnis.Entries.Count);
        Assert.Null(ergebnis.Unique);
    }
}
