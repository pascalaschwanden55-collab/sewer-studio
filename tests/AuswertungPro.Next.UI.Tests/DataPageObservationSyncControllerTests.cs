using System.Globalization;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageObservationSyncControllerTests
{
    [Fact]
    public void Sync_ignoriert_null_und_protocol_ohne_entries()
    {
        var controller = new DataPageObservationSyncController(
            Throw,
            _ => Throw(),
            _ => throw new InvalidOperationException("Keine Auswahlabfrage erwartet."),
            Throw,
            Throw,
            _ => Throw());

        controller.Sync(null);
        controller.Sync(new HaltungRecord());
        controller.Sync(new HaltungRecord
        {
            Protocol = new ProtocolDocument
            {
                Current = new ProtocolRevision { Entries = null! }
            }
        });
    }

    [Fact]
    public void Sync_uebernimmt_beobachtungen_und_markiert_dirty_refresh_autosave()
    {
        using var _ = new CultureScope(CultureInfo.InvariantCulture);
        var record = CreateRecordWithEntry("BAJA", 1.23, "Versatz");
        var dirtyCalls = 0;
        var refreshCalls = 0;
        var autoSaveCalls = 0;
        var selectedRefreshCalls = 0;
        var statusMessages = new List<string>();

        var controller = new DataPageObservationSyncController(
            () => dirtyCalls++,
            refreshed =>
            {
                Assert.Same(record, refreshed);
                refreshCalls++;
            },
            _ => false,
            () => selectedRefreshCalls++,
            () => autoSaveCalls++,
            statusMessages.Add);

        controller.Sync(record);

        Assert.Equal("1.23m BAJA Versatz", record.GetFieldValue("Primaere_Schaeden"));
        Assert.True(record.FieldMeta.TryGetValue("Primaere_Schaeden", out var meta));
        Assert.Equal(FieldSource.Manual, meta.Source);
        Assert.True(meta.UserEdited);

        var finding = Assert.Single(record.VsaFindings);
        Assert.Equal("BAJA", finding.KanalSchadencode);
        Assert.Equal("Versatz", finding.Raw);
        Assert.Equal(1.23, finding.MeterStart);

        Assert.Equal(1, dirtyCalls);
        Assert.Equal(1, refreshCalls);
        Assert.Equal(1, autoSaveCalls);
        Assert.Equal(0, selectedRefreshCalls);
        Assert.Empty(statusMessages);
    }

    [Fact]
    public void Sync_refreshes_selected_protocol_entries_und_status_nur_wenn_gewuenscht()
    {
        using var _ = new CultureScope(CultureInfo.InvariantCulture);
        var record = CreateRecordWithEntry("BAJA", 1.23, "Versatz");
        var selectedRefreshCalls = 0;
        var statusMessages = new List<string>();

        var controller = new DataPageObservationSyncController(
            () => { },
            _ => { },
            candidate => ReferenceEquals(candidate, record),
            () => selectedRefreshCalls++,
            () => { },
            statusMessages.Add);

        controller.Sync(record, showStatus: true);

        Assert.Equal(1, selectedRefreshCalls);
        Assert.Equal(new[] { "Beobachtungen in Haltungen-Feldern aktualisiert" }, statusMessages);
    }

    [Fact]
    public void Sync_macht_nichts_wenn_keine_aenderung_entsteht()
    {
        using var _ = new CultureScope(CultureInfo.InvariantCulture);
        var record = CreateRecordWithEntry("BAJA", 1.23, "Versatz");
        var dirtyCalls = 0;
        var refreshCalls = 0;
        var autoSaveCalls = 0;
        var selectedRefreshCalls = 0;
        var statusMessages = new List<string>();

        var controller = new DataPageObservationSyncController(
            () => dirtyCalls++,
            _ => refreshCalls++,
            _ => true,
            () => selectedRefreshCalls++,
            () => autoSaveCalls++,
            statusMessages.Add);

        controller.Sync(record);
        dirtyCalls = 0;
        refreshCalls = 0;
        autoSaveCalls = 0;
        selectedRefreshCalls = 0;
        statusMessages.Clear();

        controller.Sync(record, showStatus: true);

        Assert.Equal(0, dirtyCalls);
        Assert.Equal(0, refreshCalls);
        Assert.Equal(0, autoSaveCalls);
        Assert.Equal(0, selectedRefreshCalls);
        Assert.Empty(statusMessages);
    }

    [Fact]
    public void Sync_filtert_geloeschte_und_leere_codes_und_kann_bestehende_werte_leeren()
    {
        var record = new HaltungRecord
        {
            Protocol = new ProtocolDocument
            {
                Current = new ProtocolRevision
                {
                    Entries =
                    {
                        new ProtocolEntry { Code = "", Beschreibung = "ignorieren" },
                        new ProtocolEntry { Code = "BAA", Beschreibung = "geloescht", IsDeleted = true }
                    }
                }
            },
            VsaFindings =
            {
                new VsaFinding { KanalSchadencode = "ALT", Raw = "alt" }
            }
        };
        record.SetFieldValue("Primaere_Schaeden", "alt", FieldSource.Manual, userEdited: true);
        var dirtyCalls = 0;

        var controller = new DataPageObservationSyncController(
            () => dirtyCalls++,
            _ => { },
            _ => false,
            () => { },
            () => { },
            _ => { });

        controller.Sync(record);

        Assert.Equal("", record.GetFieldValue("Primaere_Schaeden"));
        Assert.Empty(record.VsaFindings);
        Assert.Equal(1, dirtyCalls);
    }

    private static HaltungRecord CreateRecordWithEntry(string code, double meterStart, string description)
        => new()
        {
            Protocol = new ProtocolDocument
            {
                Current = new ProtocolRevision
                {
                    Entries =
                    {
                        new ProtocolEntry
                        {
                            Code = code,
                            MeterStart = meterStart,
                            Beschreibung = description
                        }
                    }
                }
            }
        };

    private static void Throw()
        => throw new InvalidOperationException("Callback wurde nicht erwartet.");

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;

        public CultureScope(CultureInfo culture)
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUiCulture;
        }
    }
}
