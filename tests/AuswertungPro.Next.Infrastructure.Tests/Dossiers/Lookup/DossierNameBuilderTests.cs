using AuswertungPro.Next.Application.Dossiers.Lookup;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers.Lookup;

public sealed class DossierNameBuilderTests
{
    [Fact]
    public void Nummer_und_Nachname_ergeben_den_Namen()
    {
        Assert.Equal(
            "Liegenschaft Nr. 439 Beispiel",
            DossierNameBuilder.Build("439", "Kurt Beispiel"));
    }

    [Fact]
    public void Mehrteilige_Namen_liefern_das_letzte_Wort()
    {
        Assert.Equal(
            "Liegenschaft Nr. 439 Muster",
            DossierNameBuilder.Build("439", "Martin Peter Muster"));
    }

    [Fact]
    public void Eine_Firma_wird_ganz_uebernommen_wenn_sie_ein_Wort_ist()
    {
        Assert.Equal(
            "Liegenschaft Nr. 12 Musterbau",
            DossierNameBuilder.Build("12", "Musterbau"));
    }

    [Fact]
    public void Ohne_Eigentuemer_bleibt_nur_die_Nummer()
    {
        Assert.Equal("Liegenschaft Nr. 439", DossierNameBuilder.Build("439", null));
        Assert.Equal("Liegenschaft Nr. 439", DossierNameBuilder.Build("439", "   "));
    }

    [Fact]
    public void Zeichen_die_in_keinen_Ordnernamen_gehoeren_werden_ersetzt()
    {
        // Der Name wird auch zum Ordnernamen — ein Schraegstrich waere dort fatal.
        Assert.Equal(
            "Liegenschaft Nr. 439 Muster-Beispiel",
            DossierNameBuilder.Build("439", "Muster/Beispiel"));
    }
}
