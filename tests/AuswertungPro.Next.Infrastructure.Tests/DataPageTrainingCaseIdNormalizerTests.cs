using AuswertungPro.Next.Application.DataPage;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer <see cref="TrainingCaseIdNormalizer"/>.
/// Sichert die bisherige Logik aus DataPageViewModel.NormalizeTrainingCaseId
/// und DataPageViewModel.StripNodePrefixes ab (verhaltensneutral).
/// </summary>
public sealed class DataPageTrainingCaseIdNormalizerTests
{
    // --- NormalizeCaseId: Datums-Prefix entfernen ---

    [Theory]
    [InlineData("20250602_06.24341-35625", "06.24341-35625")]
    [InlineData("20260101_07.1028055-10.1064892", "07.1028055-10.1064892")]
    [InlineData("06.24341-35625", "06.24341-35625")]   // kein Datums-Prefix → unveraendert
    [InlineData("", "")]
    public void NormalizeCaseId_entfernt_datums_prefix(string input, string expected)
        => Assert.Equal(expected, TrainingCaseIdNormalizer.NormalizeCaseId(input));

    [Fact]
    public void NormalizeCaseId_behandelt_null_als_leerstring()
        => Assert.Equal("", TrainingCaseIdNormalizer.NormalizeCaseId(null));

    // --- StripNodePrefixes ---

    [Theory]
    [InlineData("07.1028055-10.1064892", "1028055-1064892")]   // beide Teile mit Prefix
    [InlineData("07.1028055-1064892",    "1028055-1064892")]   // nur linker Teil hat Prefix
    [InlineData("1028055-1064892",       "1028055-1064892")]   // kein Prefix → unveraendert
    [InlineData("07.1028055",            "1028055")]            // kein Bindestrich
    [InlineData("1028055",               "1028055")]            // kein Bindestrich, kein Prefix
    [InlineData("10.1064892-06.1099001", "1064892-1099001")]   // zweistelliger Prefix
    public void StripNodePrefixes_korrekt(string eingabe, string erwartet)
        => Assert.Equal(erwartet, TrainingCaseIdNormalizer.StripNodePrefixes(eingabe));
}
