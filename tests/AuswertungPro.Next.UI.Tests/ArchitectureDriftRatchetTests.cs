using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Audit 2026-08-14 (A-H1, A-M2): Zwei Muster wachsen still weiter, obwohl beide
/// bereits als Schuld erkannt sind. Diese Waechter bauen nichts um — sie halten den
/// Bestand fest, damit er nur noch kleiner werden kann.
///
/// <para>1. Der konkrete <c>ServiceProvider</c> als Konstruktorparameter ist ein
/// Service-Locator. <see cref="UiArchitectureGuardTests"/> verbietet bereits
/// <c>App.Services</c> — derselbe Mechanismus laeuft als Parameter ungehindert
/// weiter. Wer ihn nimmt, erklaert seine Abhaengigkeiten nicht mehr, und jeder Test
/// muss den ganzen Container bauen.</para>
///
/// <para>2. Statische <c>Current</c>-Fassaden sind der zweite Weg an der
/// Registrierung vorbei. Neuentwicklung geht ueber ein Interface im Konstruktor.</para>
/// </summary>
public sealed class ArchitectureDriftRatchetTests
{
    // Bestand am 2026-08-15. Diese Liste darf schrumpfen, niemals wachsen.
    // Wer hier einen Namen ergaenzen will, baut stattdessen den Konstruktor um.
    private static readonly string[] ViewModelsMitServiceProvider =
    {
        "ViewModels/Pages/BuilderPageViewModel.cs",
        "ViewModels/Pages/DataPageViewModel.cs",
        "ViewModels/Pages/ExportPageViewModel.cs",
        "ViewModels/Pages/ImportPageViewModel.cs",
        "ViewModels/Pages/KarteViewModel.cs",
        "ViewModels/Pages/MediaConflictsPageViewModel.cs",
        "ViewModels/Pages/OverviewPageViewModel.cs",
        "ViewModels/Pages/ProjectPageViewModel.cs",
        "ViewModels/Pages/SanierungsMatrixPageViewModel.cs",
        "ViewModels/Pages/SchachtSanierungsMatrixPageViewModel.cs",
        "ViewModels/Pages/SchaechtePageViewModel.cs",
        "ViewModels/Pages/SchattenauswertungPageViewModel.cs",
        "ViewModels/Pages/SettingsPageViewModel.cs",
        "ViewModels/Pages/VsaPageViewModel.cs",
        "ViewModels/ShellViewModel.cs",
    };

    /// <summary>
    /// Bestand am 2026-08-15. Bewusst eine Zahl statt 51 Dateinamen: Der Wert soll
    /// beim Aufraeumen sinken, ohne dass jedes Mal eine Liste gepflegt wird.
    /// </summary>
    private const int CurrentFassadenBestand = 51;

    private static readonly Regex ServiceProviderParameter = new(
        @"[(,]\s*ServiceProvider\s+[a-z_]", RegexOptions.Compiled);

    private static readonly Regex CurrentFassade = new(
        @"public\s+static\s+(?:readonly\s+)?[\w<>?.\[\]]+\s+Current\b", RegexOptions.Compiled);

    [Fact]
    public void Keine_neuen_ViewModels_mit_ServiceProvider_im_Konstruktor()
    {
        var uiRoot = RepoFile("src", "AuswertungPro.Next.UI");
        var viewModelRoot = Path.Combine(uiRoot, "ViewModels");
        var erlaubt = new HashSet<string>(ViewModelsMitServiceProvider, StringComparer.OrdinalIgnoreCase);

        var gefunden = Directory.EnumerateFiles(viewModelRoot, "*.cs", SearchOption.AllDirectories)
            .Where(pfad => !IstBuildAusgabe(pfad))
            .Where(pfad => ServiceProviderParameter.IsMatch(File.ReadAllText(pfad)))
            .Select(pfad => RelativZuUi(uiRoot, pfad))
            .OrderBy(pfad => pfad, StringComparer.Ordinal)
            .ToArray();

        var neu = gefunden.Where(pfad => !erlaubt.Contains(pfad)).ToArray();
        Assert.True(neu.Length == 0,
            "Neue ViewModels nehmen den konkreten ServiceProvider im Konstruktor. Statt dessen die " +
            "wirklich genutzten Dienste als Interfaces uebergeben:\n" + string.Join("\n", neu));

        var verschwunden = erlaubt.Except(gefunden, StringComparer.OrdinalIgnoreCase).ToArray();
        Assert.True(verschwunden.Length == 0,
            "Aufgeraeumt - bitte diese Namen aus ViewModelsMitServiceProvider entfernen, damit der " +
            "Waechter scharf bleibt:\n" + string.Join("\n", verschwunden));
    }

    [Fact]
    public void Die_Zahl_der_statischen_Current_Fassaden_waechst_nicht()
    {
        var wurzeln = new[]
        {
            RepoFile("src", "AuswertungPro.Next.Infrastructure"),
            RepoFile("src", "AuswertungPro.Next.UI"),
        };

        var treffer = wurzeln
            .SelectMany(wurzel => Directory.EnumerateFiles(wurzel, "*.cs", SearchOption.AllDirectories))
            .Where(pfad => !IstBuildAusgabe(pfad))
            .Where(pfad => CurrentFassade.IsMatch(File.ReadAllText(pfad)))
            .ToArray();

        Assert.True(treffer.Length <= CurrentFassadenBestand,
            $"Neue statische Current-Fassade(n): {treffer.Length} statt hoechstens {CurrentFassadenBestand}. " +
            "Neue Dienste per Interface in den Konstruktor geben, nicht als globalen Zugriffspunkt.");

        Assert.True(treffer.Length == CurrentFassadenBestand,
            $"Es sind nur noch {treffer.Length} Current-Fassaden (Bestand war {CurrentFassadenBestand}). " +
            "Bitte CurrentFassadenBestand auf den neuen, kleineren Wert setzen - sonst faengt die " +
            "Ratsche den naechsten Zuwachs zu spaet.");
    }

    private static bool IstBuildAusgabe(string pfad)
    {
        var norm = Path.GetFullPath(pfad).Replace('\\', '/');
        return norm.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
               || norm.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    private static string RelativZuUi(string uiRoot, string pfad)
        => Path.GetRelativePath(uiRoot, pfad).Replace('\\', '/');
}
