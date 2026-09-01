using System.Text.RegularExpressions;
using static AuswertungPro.Next.Infrastructure.Tests.TestRepoPaths;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class AiUserTextEncodingGuardTests
{
    [Theory]
    [InlineData("src", "AuswertungPro.Next.Infrastructure", "Ai", "FullProtocolGenerationService.cs")]
    [InlineData("src", "AuswertungPro.Next.Infrastructure", "Ai", "VideoFullAnalysisService.cs")]
    [InlineData("src", "AuswertungPro.Next.Infrastructure", "Ai", "VideoAnalysisPipelineService.cs")]
    public void Sichtbare_KI_Texte_enthalten_keine_Mojibake_Zeichen(params string[] path)
    {
        var source = File.ReadAllText(RepoFile(path));
        var stringLiterals = Regex.Matches(source, "\"(?:\\\\.|[^\"\\\\])*\"")
            .Select(match => match.Value)
            .ToArray();

        Assert.DoesNotContain(stringLiterals, literal =>
            literal.Contains('Ã')
            || literal.Contains('�')
            || literal.Contains("â€“", StringComparison.Ordinal));
    }
}
