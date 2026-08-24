namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Die ausgelieferte Word-Vorlage des Eigentuemerdossiers.
///
/// Sie ist eine echte, von Hand gestaltete Word-Datei — wie
/// "Export_Vorlage\Haltungen.xlsx" und "Export_Vorlage\Schaechte.xlsx". Sie
/// wird NICHT aus Code erzeugt: Deckblatt, Textfelder, Schriften, Logo und
/// Wappen stammen aus dem Originaldossier und liessen sich programmatisch
/// nicht originalgetreu nachbauen.
///
/// Geaendert wird sie in Word selbst. Stehen bleiben muessen nur die
/// Platzhalter <c>{{Name}}</c>, die Bildmarke <c>{{@Uebersichtsplan}}</c> und
/// die drei Wiederholzeilen <c>{{#Aenderungen}}</c>, <c>{{#Eigentuemer}}</c>
/// und <c>{{#Themen}}</c>. Ein Waechtertest haelt den Bestand fest.
/// </summary>
public static class DossierWordTemplate
{
    /// <summary>Dateiname der Vorlage im Ordner "Export_Vorlage".</summary>
    public const string TemplateFileName = "Eigentuemerdossier.docx";
}
