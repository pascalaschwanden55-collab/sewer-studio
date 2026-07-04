using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.QgisBridge;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Der Payload-Cache der QGIS-Bridge haengt an diesen Fingerprints:
/// gleiche Daten muessen denselben Fingerprint liefern (Cache-Treffer),
/// jede relevante Aenderung einen anderen (Cache-Verwurf).
/// </summary>
public sealed class QgisPayloadFingerprintTests
{
    private const long Ticks = 123_456_789;

    [Fact]
    public void Gleiche_daten_ergeben_gleichen_fingerprint()
    {
        var a = QgisProjectSnapshot.Capture(CreateProject(), "A-B");
        var b = QgisProjectSnapshot.Capture(CreateProject(), "A-B");

        Assert.Equal(a.NetworkFingerprint(Ticks), b.NetworkFingerprint(Ticks));
        Assert.Equal(a.DamagesFingerprint(Ticks), b.DamagesFingerprint(Ticks));
        Assert.Equal(a.CurrentFingerprint(Ticks), b.CurrentFingerprint(Ticks));
    }

    [Fact]
    public void Zustandsklasse_aendert_network_fingerprint()
    {
        var before = QgisProjectSnapshot.Capture(CreateProject(), null).NetworkFingerprint(Ticks);

        var project = CreateProject();
        project.Data[0].SetFieldValue("Zustandsklasse", "1", FieldSource.Manual, userEdited: true);
        var after = QgisProjectSnapshot.Capture(project, null).NetworkFingerprint(Ticks);

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void Neuer_schaden_aendert_damages_fingerprint()
    {
        var before = QgisProjectSnapshot.Capture(CreateProject(), null).DamagesFingerprint(Ticks);

        var project = CreateProject();
        project.Data[0].VsaFindings.Add(new VsaFinding { KanalSchadencode = "BBA", MeterStart = 7.5 });
        var after = QgisProjectSnapshot.Capture(project, null).DamagesFingerprint(Ticks);

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void Haltungswechsel_aendert_current_fingerprint()
    {
        var project = CreateProject();
        var a = QgisProjectSnapshot.Capture(project, "A-B").CurrentFingerprint(Ticks);
        var b = QgisProjectSnapshot.Capture(project, "C-D").CurrentFingerprint(Ticks);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Neues_xtf_aendert_alle_fingerprints()
    {
        var snapshot = QgisProjectSnapshot.Capture(CreateProject(), "A-B");

        Assert.NotEqual(snapshot.NetworkFingerprint(1), snapshot.NetworkFingerprint(2));
        Assert.NotEqual(snapshot.DamagesFingerprint(1), snapshot.DamagesFingerprint(2));
        Assert.NotEqual(snapshot.CurrentFingerprint(1), snapshot.CurrentFingerprint(2));
    }

    private static Project CreateProject()
    {
        var project = new Project { Name = "Fingerprint-Test" };
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "A-B", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Zustandsklasse", "4", FieldSource.Manual, userEdited: true);
        record.VsaFindings.Add(new VsaFinding { KanalSchadencode = "BAB", MeterStart = 5.0 });
        project.Data.Add(record);
        return project;
    }
}
