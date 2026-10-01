using System;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Die Grundbuchauskunft liefert Namen und Wohnadressen echter Personen. Der
/// Wert gehoert ins Projekt — die Logdatei dagegen wandert in Diagnosepakete
/// und Sicherungen. Dort hat er nichts verloren.
///
/// Ein statischer Waechter, bewusst: Er soll verhindern, dass jemand spaeter
/// beim Fehlersuchen schnell einen Namen ins Protokoll schreibt.
/// </summary>
public sealed class FeldNachschlagLogTests
{
    /// <summary>
    /// Die Traeger echter Personendaten. Bewusst nicht das blosse ".Name" —
    /// das trifft auch ex.GetType().Name, den Typnamen einer Ausnahme, und
    /// waere ein Fehlalarm.
    /// </summary>
    private static readonly string[] VerboteneAngaben =
    [
        "owner.Name",
        "o.Name",
        "Owners",
        "AddressLine",
        "BuildingStreet",
        "BuildingHouseNumber",
        "eintrag.",
        "vorschlag.Wert",
        "Vorschlag.Wert",
        "parzelle.Number"
    ];

    [Fact]
    public void Der_Grundbuchweg_protokolliert_keine_Personendaten()
    {
        var quelle = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.Infrastructure", "Lookup", "GrundbuchFeldNachschlag.cs"));

        var logzeilen = quelle
            .Split('\n')
            .Where(z => z.Contains("_log", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(logzeilen);

        foreach (var zeile in logzeilen)
        {
            foreach (var verboten in VerboteneAngaben)
            {
                Assert.DoesNotContain(verboten, zeile, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Es_gibt_keinen_Sammellauf()
    {
        // Die Grundbuchauskunft erlaubt ausdruecklich nur Einzelabfragen mit
        // Bestaetigung. Ein Knopf "alle leeren Felder fuellen" waere genau das,
        // was sie verbietet.
        var wurzeln = new[]
        {
            RepoFile("src", "AuswertungPro.Next.UI"),
            RepoFile("src", "AuswertungPro.Next.Application"),
            RepoFile("src", "AuswertungPro.Next.Infrastructure")
        };

        foreach (var wurzel in wurzeln)
        {
            foreach (var datei in Directory.EnumerateFiles(wurzel, "*.cs", SearchOption.AllDirectories))
            {
                if (datei.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                    || datei.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var text = File.ReadAllText(datei);
                Assert.DoesNotContain("AlleFelderNachschlagen", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("NachschlagAlle", text, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
