namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Trait-Namen für den Schnelllauf. Testklassen, deren Tests einen Kindprozess starten
/// (<c>WpfIsolatedTestProcess.RunAsync</c>), tragen <c>[Trait(TestKategorie.Name, TestKategorie.Kindprozess)]</c>.
/// Schnelllauf: <c>--filter "Kategorie!=Kindprozess"</c>. Pre-Push-Hook und CI lassen alles laufen.
/// </summary>
internal static class TestKategorie
{
    public const string Name = "Kategorie";
    public const string Kindprozess = "Kindprozess";
}
