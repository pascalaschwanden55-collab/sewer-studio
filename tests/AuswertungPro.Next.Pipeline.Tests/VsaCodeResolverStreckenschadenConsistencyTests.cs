using System.Text.Json;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.VsaCatalog;
using AuswertungPro.Next.Infrastructure.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

[Collection(VsaCodeResolverTestCollection.Name)]
public sealed class VsaCodeResolverStreckenschadenConsistencyTests
{
    [Fact]
    public void IsStreckenschadenCode_MatchesCatalogRangeOrDomainClassifier_ForAllManifestCodes()
    {
        var codes = LoadManifestCodes();
        VsaCodeResolver.ConfigureCatalog(new InMemoryCodeCatalogProvider(codes));

        var mismatches = codes
            .Select(def =>
            {
                var expected = def.RequiresRange || StreckenschadenCodeClassifier.IsStreckenschadenCode(def.Code);
                var actual = VsaCodeResolver.IsStreckenschadenCode(def.Code);
                return new { def.Code, Expected = expected, Actual = actual };
            })
            .Where(x => x.Expected != x.Actual)
            .Select(x => $"{x.Code}: expected={x.Expected}, actual={x.Actual}")
            .ToArray();

        Assert.Empty(mismatches);
    }

    private static IReadOnlyList<CodeDefinition> LoadManifestCodes()
    {
        var path = FindManifestPath();
        var json = File.ReadAllText(path);
        var document = JsonSerializer.Deserialize<CodeCatalogDocument>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return document?.Codes.ToArray() ?? Array.Empty<CodeDefinition>();
    }

    private static string FindManifestPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(
                current.FullName,
                "src",
                "AuswertungPro.Next.UI",
                "Data",
                "vsa_kek_2020_catalog_manifest.json");

            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        throw new FileNotFoundException("VSA-KEK-Katalogmanifest wurde nicht gefunden.");
    }

    private sealed class InMemoryCodeCatalogProvider : ICodeCatalogProvider
    {
        private readonly IReadOnlyList<CodeDefinition> _codes;

        public InMemoryCodeCatalogProvider(IReadOnlyList<CodeDefinition> codes)
        {
            _codes = codes;
        }

        public IReadOnlyList<CodeDefinition> GetAll() => _codes;

        public bool TryGet(string code, out CodeDefinition def)
        {
            def = _codes.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))
                ?? new CodeDefinition();
            return !string.IsNullOrWhiteSpace(def.Code);
        }

        public void Save(IReadOnlyList<CodeDefinition> codes)
            => throw new NotSupportedException();

        public IReadOnlyList<string> AllowedCodes()
            => _codes.Select(c => c.Code).ToList();

        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null)
            => Array.Empty<string>();
    }
}
