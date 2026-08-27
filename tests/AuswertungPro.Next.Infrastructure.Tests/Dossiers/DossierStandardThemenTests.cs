using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Die Themenzeilen des Dossiers stammen aus dem Gebiet. Ein Gebiet ohne Themen
/// erzeugte deshalb eine leere Tabelle „Informationen Sanierung" - real passiert
/// im Projekt Feldliweg (0 Gebietsthemen, 15 Dossiers). Die Regel „ohne Themen gilt
/// die Standardliste" stand bisher nur im Gebietsfenster und wirkte nur, wenn man
/// den Dialog oeffnete UND speicherte.
/// </summary>
public sealed class DossierStandardThemenTests
{
    [Fact]
    public void Ein_Gebiet_ohne_Themen_bekommt_die_Standardliste()
    {
        var document = new DossierDocument
        {
            SchemaVersion = DossierDocument.CurrentSchemaVersion,
            Area = new DossierAreaSettings { Topics = new List<DossierTopicRow>() }
        };

        DossierDocumentMigration.MigrateToCurrent(document);

        Assert.Equal(
            DossierDocumentMigration.DefaultTopicTitles,
            document.Area!.Topics!.Select(t => t.Title).ToList());
    }

    [Fact]
    public void Ein_Gebiet_mit_eigenen_Themen_bleibt_unveraendert()
    {
        // Jagdmatt hat 11 gepflegte Themen mit eigenen Texten - die duerfen weder
        // ergaenzt noch ueberschrieben werden.
        var document = new DossierDocument
        {
            SchemaVersion = DossierDocument.CurrentSchemaVersion,
            Area = new DossierAreaSettings
            {
                Topics =
                [
                    new DossierTopicRow { Title = "Ausgangslage", Text = "Eigener Text" },
                    new DossierTopicRow { Title = "Nur bei uns", Text = "Zweiter Text" }
                ]
            }
        };

        DossierDocumentMigration.MigrateToCurrent(document);

        Assert.Equal(2, document.Area!.Topics!.Count);
        Assert.Equal("Ausgangslage", document.Area.Topics[0].Title);
        Assert.Equal("Eigener Text", document.Area.Topics[0].Text);
        Assert.Equal("Nur bei uns", document.Area.Topics[1].Title);
    }

    [Fact]
    public void Die_Standardliste_traegt_die_stehenden_Texte()
    {
        var document = new DossierDocument
        {
            SchemaVersion = DossierDocument.CurrentSchemaVersion,
            Area = new DossierAreaSettings { Topics = new List<DossierTopicRow>() }
        };

        DossierDocumentMigration.MigrateToCurrent(document);

        var texte = document.Area!.Topics!.ToDictionary(t => t.Title!, t => t.Text ?? "");

        Assert.Contains("Abwasser Uri", texte["Ausgangslage"]);
        Assert.Contains("Provisorien", texte["Behinderungen, Zugänge, Verkehrsführung, Fussgängerführung"]);
        Assert.Contains("Versicherungen", texte["Bemerkungen"]);
        Assert.Contains("TV-Haltungsprotokolle", texte["Beilagen"]);
    }

    [Fact]
    public void Die_Ausgangslage_nennt_keinen_festen_Ort_sondern_einen_optionalen_Perimeter()
    {
        // Der Ursprungstext nannte woertlich „im Perimeter der Linden und
        // Lindenstrasse". In einem Dossier fuer ein anderes Gebiet waere das falsch.
        var ausgangslage = DossierDocumentMigration.DefaultTopicTexts["Ausgangslage"];

        Assert.DoesNotContain("Lindenstrasse", ausgangslage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("{{Gebiet_Perimeter}}", ausgangslage, StringComparison.Ordinal);
    }
}
