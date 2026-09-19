using AuswertungPro.Next.Application.UseCases.Verteilung;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Anlass 18.09.2026: Der gespeicherte Verteilordner zeigte auf "I:\", ein Laufwerk das es
/// nicht mehr gab. Jede der acht Dichtheits-PDFs meldete daraufhin nur
/// "Could not find a part of the path 'I:\...'". Die Wurzel wird jetzt vorher geprueft.
/// Geprueft wird der Laufwerks-/Freigabestamm, nicht der ganze Pfad: ein noch nicht
/// vorhandener Unterordner wird beim Verteilen ganz normal angelegt.
/// </summary>
public sealed class VerteilzielPruefungTests
{
    [Fact]
    public void Pruefe_FehlendesLaufwerk_IstNichtErreichbarUndNenntDenStamm()
    {
        var ergebnis = VerteilzielPruefung.Pruefe(@"I:\", _ => false);

        Assert.False(ergebnis.Erreichbar);
        Assert.Contains(@"I:\", ergebnis.Meldung);
    }

    [Fact]
    public void Pruefe_FehlendesLaufwerkTieferPfad_NenntEbenfallsDenStamm()
    {
        var ergebnis = VerteilzielPruefung.Pruefe(@"I:\Protokolle\Verteilt", _ => false);

        Assert.False(ergebnis.Erreichbar);
        Assert.Contains(@"I:\", ergebnis.Meldung);
        Assert.Contains(@"I:\Protokolle\Verteilt", ergebnis.Meldung);
    }

    [Fact]
    public void Pruefe_LaufwerkDaOrdnerFehlt_IstErreichbar()
    {
        // Der Zielordner darf fehlen - er wird beim Verteilen angelegt.
        var ergebnis = VerteilzielPruefung.Pruefe(
            @"D:\Projekte\Neues_Projekt\Haltungen_Verteilt",
            pfad => string.Equals(pfad, @"D:\", StringComparison.OrdinalIgnoreCase));

        Assert.True(ergebnis.Erreichbar);
        Assert.Null(ergebnis.Meldung);
    }

    [Fact]
    public void Pruefe_KeineWurzelKonfiguriert_IstErreichbar()
    {
        // Leere Wurzel heisst "nicht konfiguriert"; der Aufrufer nimmt dann den Projektordner.
        var ergebnis = VerteilzielPruefung.Pruefe("   ", _ => false);

        Assert.True(ergebnis.Erreichbar);
        Assert.Null(ergebnis.Meldung);
    }

    [Fact]
    public void Pruefe_NetzfreigabeFehlt_IstNichtErreichbar()
    {
        var ergebnis = VerteilzielPruefung.Pruefe(@"\server\freigabe\verteilt", _ => false);

        Assert.False(ergebnis.Erreichbar);
        Assert.Contains(@"\server\freigabe", ergebnis.Meldung);
    }
}
