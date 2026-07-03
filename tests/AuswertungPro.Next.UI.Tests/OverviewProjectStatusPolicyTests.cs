using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Status-Badge der Projektuebersicht. Hintergrund-Bug: Nach "Projekt wechseln"
/// (Launcher) zeigte der Badge "Projekt noch nicht gespeichert" fuer ein komplett
/// gespeichertes Projekt, weil er am Navigations-Zustand (IsProjectReady) hing
/// statt am echten "hat eine Datei auf der Platte"-Zustand.
/// </summary>
public sealed class OverviewProjectStatusPolicyTests
{
    [Fact]
    public void Build_UngespeicherteAenderungen_WennDirty()
    {
        Assert.Equal(
            "Ungespeicherte Aenderungen",
            OverviewProjectStatusPolicy.Build(isDirty: true, hasPersistedProject: true));
    }

    [Fact]
    public void Build_UngespeicherteAenderungen_HatVorrangVorNichtGespeichert()
    {
        Assert.Equal(
            "Ungespeicherte Aenderungen",
            OverviewProjectStatusPolicy.Build(isDirty: true, hasPersistedProject: false));
    }

    [Fact]
    public void Build_ProjektGespeichert_WennDateiAufPlatteUndNichtDirty()
    {
        Assert.Equal(
            "Projekt gespeichert",
            OverviewProjectStatusPolicy.Build(isDirty: false, hasPersistedProject: true));
    }

    [Fact]
    public void Build_NochNichtGespeichert_NurWennKeineDateiAufPlatte()
    {
        Assert.Equal(
            "Projekt noch nicht gespeichert",
            OverviewProjectStatusPolicy.Build(isDirty: false, hasPersistedProject: false));
    }
}
