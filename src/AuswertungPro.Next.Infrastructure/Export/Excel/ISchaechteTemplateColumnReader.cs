namespace AuswertungPro.Next.Infrastructure.Export.Excel;

/// <summary>
/// Liest die bearbeitbaren Schachtspalten aus der ausgelieferten Excel-Vorlage.
/// </summary>
public interface ISchaechteTemplateColumnReader
{
    SchaechteTemplateColumnReadResult LoadFromExportDirectory(string baseDirectory);

    IReadOnlyList<string> ReadColumns(string templatePath);
}
