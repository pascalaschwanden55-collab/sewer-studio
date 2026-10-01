using System.IO;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Waechter zur Optikanalyse 28.09.2026, Aufgabe 5 («Programmidentitaet»): kein sichtbarer
/// XAML-Text im UI-Projekt darf mehr «Sewer Studio» (mit Leerzeichen, alte Schreibweise) oder
/// «AuswertungPro» (alter interner Projektname) enthalten - die verbindliche Schreibweise ist
/// «SewerStudio». Geprueft werden nur echte Anzeige-Attribute (<c>Text</c>, <c>Content</c>,
/// <c>Title</c>, <c>Header</c>, <c>ToolTip</c>) - <c>x:Class</c>/<c>xmlns</c>-Deklarationen tragen
/// den .NET-Namensraum <c>AuswertungPro.Next.UI...</c> und sind bewusst nicht Teil dieser Pruefung
/// (kein Nutzer sieht sie). C#-Quellcode ist ebenfalls bewusst ausgenommen: dort steht
/// «AuswertungPro» programmweit in jeder Namensraumdeklaration und jedem <c>using</c> - eine
/// Textsuche dort waere kein sinnvoller Wächter fuer sichtbare Oberflaechentexte.
/// </summary>
public sealed class DesignAuditProgrammidentitaetTests
{
    private static readonly string UiRoot = RepoFile("src", "AuswertungPro.Next.UI");

    private static readonly Regex SichtbaresAttribut =
        new(@"\b(?:Text|Content|Title|Header|ToolTip)=""([^""]*)""", RegexOptions.Compiled);

    private static readonly string[] VerboteneBegriffe = ["Sewer Studio", "AuswertungPro"];

    [Fact]
    public void Keine_sichtbare_Xaml_Zeichenkette_traegt_die_alte_Schreibweise()
    {
        var verstoesse = new List<string>();

        // TestXaml.Alle() laesst Build-Ausgaben (bin/obj) weg: Sie enthalten Kopien alter Staende und
        // gehoeren nicht zum gepflegten Quellbestand.
        foreach (var pfad in TestXaml.Alle())
        {
            var xaml = File.ReadAllText(pfad);
            foreach (Match treffer in SichtbaresAttribut.Matches(xaml))
            {
                var wert = treffer.Groups[1].Value;
                foreach (var begriff in VerboteneBegriffe)
                {
                    if (wert.Contains(begriff, StringComparison.Ordinal))
                    {
                        verstoesse.Add(
                            $"{Path.GetRelativePath(UiRoot, pfad)}: Attributwert \"{wert}\" enthält \"{begriff}\"");
                    }
                }
            }
        }

        Assert.True(verstoesse.Count == 0,
            "Alte Schreibweise in sichtbarem XAML-Text gefunden:\n" + string.Join("\n", verstoesse));
    }

    /// <summary>
    /// Sabotageprobe (Beleg, dass der Waechter wirklich prueft): eine bekannt falsche
    /// Attributschreibweise muss anschlagen.
    /// </summary>
    [Fact]
    public void Der_Waechter_erkennt_die_alte_Schreibweise_wirklich()
    {
        var xamlMitAlterSchreibweise = "<TextBlock Text=\"SEWER STUDIO\" Title=\"Sewer Studio\"/>";
        var treffer = SichtbaresAttribut.Matches(xamlMitAlterSchreibweise);
        Assert.Equal(2, treffer.Count);
        Assert.Contains(treffer, m => m.Groups[1].Value == "Sewer Studio");
    }
}
