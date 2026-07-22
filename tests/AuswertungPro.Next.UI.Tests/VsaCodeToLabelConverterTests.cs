using System;
using System.Collections.Generic;
using System.Globalization;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Prueft den neuen "nur Klartext"-Converter fuer eigene Bedeutung-Spalten.
/// UI.Tests laeuft sequenziell (DisableTestParallelization), darum ist das
/// Setzen des globalen Katalogs mit Wiederherstellung im finally unbedenklich.
/// </summary>
public sealed class VsaCodeToLabelConverterTests
{
    [Fact]
    public void Gibt_leeren_String_fuer_null_leer_oder_leeren_Text()
    {
        var converter = new VsaCodeToLabelConverter();

        Assert.Equal(string.Empty, converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal(string.Empty, converter.Convert("", typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal(string.Empty, converter.Convert("   ", typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Gibt_nur_den_Klartext_fuer_bekannten_Code_ohne_Code_Praefix()
    {
        var vorher = VsaCodeResolver.CurrentCatalog;
        try
        {
            VsaCodeResolver.ConfigureCatalog(new TestKatalog());
            var converter = new VsaCodeToLabelConverter();

            // BCAEA ist nicht exakt im Katalog → Fallback auf den Hauptcode BCA.
            var result = converter.Convert("BCAEA", typeof(string), null, CultureInfo.InvariantCulture);

            Assert.Equal("Seitlicher Anschluss", result);
        }
        finally
        {
            VsaCodeResolver.ConfigureCatalog(vorher);
        }
    }

    [Fact]
    public void Unbekannter_Code_bei_gesetztem_Katalog_bleibt_leer()
    {
        var vorher = VsaCodeResolver.CurrentCatalog;
        try
        {
            VsaCodeResolver.ConfigureCatalog(new TestKatalog());
            var converter = new VsaCodeToLabelConverter();

            var result = converter.Convert("ZZZ", typeof(string), null, CultureInfo.InvariantCulture);

            Assert.Equal(string.Empty, result);
        }
        finally
        {
            VsaCodeResolver.ConfigureCatalog(vorher);
        }
    }

    private sealed class TestKatalog : ICodeCatalogProvider
    {
        private static readonly CodeDefinition Bca = new()
        {
            Code = "BCA",
            Title = "Seitlicher Anschluss",
            CanonicalCode = "BCA",
            IsSelectable = true
        };

        public IReadOnlyList<CodeDefinition> GetAll() => new[] { Bca };

        public bool TryGet(string code, out CodeDefinition def)
        {
            if (string.Equals(code, "BCA", StringComparison.OrdinalIgnoreCase))
            {
                def = Bca;
                return true;
            }

            def = new CodeDefinition();
            return false;
        }

        public void Save(IReadOnlyList<CodeDefinition> codes) => throw new NotSupportedException();

        public IReadOnlyList<string> AllowedCodes() => new[] { "BCA" };

        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null)
            => Array.Empty<string>();
    }
}
