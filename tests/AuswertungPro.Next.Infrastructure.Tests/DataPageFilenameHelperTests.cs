using AuswertungPro.Next.Application.DataPage;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer <see cref="DataPageFilenameHelper.SanitizeFilenamePart"/>.
/// Sichert die bisherige Logik aus DataPageViewModel.SanitizeFilenamePart ab (verhaltensneutral).
/// </summary>
public sealed class DataPageFilenameHelperTests
{
    [Theory]
    [InlineData(null, "unknown")]
    [InlineData("", "unknown")]
    [InlineData("  ", "unknown")]
    public void SanitizeFilenamePart_liefert_unknown_bei_leer(string? input, string expected)
        => Assert.Equal(expected, DataPageFilenameHelper.SanitizeFilenamePart(input));

    [Theory]
    [InlineData("Haltung_42", "Haltung_42")]
    [InlineData("Haltungsname", "Haltungsname")]
    [InlineData("ABC-123", "ABC-123")]
    public void SanitizeFilenamePart_laesst_gueltigen_name_unveraendert(string input, string expected)
        => Assert.Equal(expected, DataPageFilenameHelper.SanitizeFilenamePart(input));

    [Theory]
    [InlineData("Haltung/42", "Haltung_42")]
    [InlineData("Haltung\\42", "Haltung_42")]
    [InlineData("Name:Wert", "Name_Wert")]
    [InlineData("A*B?C", "A_B_C")]
    public void SanitizeFilenamePart_ersetzt_ungueltige_zeichen_durch_unterstrich(string input, string expected)
        => Assert.Equal(expected, DataPageFilenameHelper.SanitizeFilenamePart(input));
}
