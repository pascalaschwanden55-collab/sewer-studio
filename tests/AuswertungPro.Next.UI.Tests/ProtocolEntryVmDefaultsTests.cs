using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.ViewModels.Protocol;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProtocolEntryVmDefaultsTests
{
    [Fact]
    public void EnsureVsaDefaults_bleibt_bei_leerem_Eintrag_ohne_Metadaten_Aenderung()
    {
        var entry = new ProtocolEntry();
        var viewModel = new ProtocolEntryVM(entry);
        var parameterNotifications = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ProtocolEntryVM.Parameters))
                parameterNotifications++;
        };

        viewModel.EnsureVsaDefaults();

        Assert.Null(entry.CodeMeta);
        Assert.Equal(0, parameterNotifications);
    }

    [Fact]
    public void EnsureVsaDefaults_ergaenzt_Code_Meter_und_Zeit_Aliase_genau_einmal()
    {
        var entry = new ProtocolEntry
        {
            Code = "BABAC",
            MeterStart = 1.2,
            Zeit = new TimeSpan(1, 2, 3)
        };
        var viewModel = new ProtocolEntryVM(entry);
        var parameterNotifications = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ProtocolEntryVM.Parameters))
                parameterNotifications++;
        };

        viewModel.EnsureVsaDefaults();
        var notificationsAfterFirstCall = parameterNotifications;
        viewModel.EnsureVsaDefaults();

        var parameters = Assert.IsType<Dictionary<string, string>>(entry.CodeMeta!.Parameters);
        Assert.Equal("BABAC", parameters["vsa.code"]);
        Assert.Equal("BABAC", parameters["Code"]);
        Assert.Equal("1.20", parameters["vsa.distanz"]);
        Assert.Equal("1.20", parameters["Distance"]);
        Assert.Equal("01:02:03", parameters["vsa.video"]);
        Assert.Equal("01:02:03", parameters["TimeCtr"]);
        Assert.True(notificationsAfterFirstCall > 0);
        Assert.Equal(notificationsAfterFirstCall, parameterNotifications);
    }

    [Fact]
    public void EnsureVsaDefaults_respektiert_vorhandene_Aliaswerte()
    {
        var entry = new ProtocolEntry
        {
            Code = "BABAC",
            MeterStart = 1.2,
            Zeit = new TimeSpan(1, 2, 3),
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Code"] = "ALT",
                    ["Distance"] = "7,5",
                    ["TimeCtr"] = "9:10"
                }
            }
        };
        var viewModel = new ProtocolEntryVM(entry);
        var parameterNotifications = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ProtocolEntryVM.Parameters))
                parameterNotifications++;
        };

        viewModel.EnsureVsaDefaults();

        var parameters = entry.CodeMeta.Parameters;
        Assert.Equal("ALT", parameters["Code"]);
        Assert.Equal("7,5", parameters["Distance"]);
        Assert.Equal("9:10", parameters["TimeCtr"]);
        Assert.DoesNotContain("vsa.code", parameters.Keys);
        Assert.DoesNotContain("vsa.distanz", parameters.Keys);
        Assert.DoesNotContain("vsa.video", parameters.Keys);
        Assert.Equal(0, parameterNotifications);
    }

    [Theory]
    [InlineData(null, "A1")]
    [InlineData("", "A1")]
    [InlineData(" a ", "A1")]
    [InlineData("A1", "A1")]
    [InlineData("b12", "B12")]
    [InlineData("C003", "C003")]
    [InlineData("D1", "A1")]
    [InlineData("A-1", "A1")]
    [InlineData("A1x", "A1")]
    public void ApplyStreckenLogik_normalisiert_oder_setzt_den_bisherigen_Fallback(
        string? raw,
        string expected)
    {
        var entry = new ProtocolEntry
        {
            Code = "BABAC",
            IsStreckenschaden = true,
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            }
        };
        if (raw is not null)
            entry.CodeMeta.Parameters["vsa.strecke"] = raw;
        var viewModel = new ProtocolEntryVM(entry);
        var parameterNotifications = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ProtocolEntryVM.Parameters))
                parameterNotifications++;
        };

        viewModel.ApplyStreckenLogik();

        Assert.Equal(expected, entry.CodeMeta.Parameters["vsa.strecke"]);
        Assert.True(parameterNotifications > 0);
    }

    [Fact]
    public void ApplyStreckenLogik_entfernt_Strecke_bei_Einzelschaden()
    {
        var entry = new ProtocolEntry
        {
            Code = "BABAC",
            IsStreckenschaden = false,
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["vsa.strecke"] = "B2"
                }
            }
        };
        var viewModel = new ProtocolEntryVM(entry);
        var parameterNotifications = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ProtocolEntryVM.Parameters))
                parameterNotifications++;
        };

        viewModel.ApplyStreckenLogik();

        Assert.DoesNotContain("vsa.strecke", entry.CodeMeta.Parameters.Keys);
        Assert.True(parameterNotifications > 0);
    }

    [Fact]
    public void ApplyStreckenLogik_legt_bei_Einzelschaden_weiterhin_leere_Metadaten_an()
    {
        var entry = new ProtocolEntry
        {
            Code = "BABAC",
            IsStreckenschaden = false
        };
        var viewModel = new ProtocolEntryVM(entry);
        var parameterNotifications = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ProtocolEntryVM.Parameters))
                parameterNotifications++;
        };

        viewModel.ApplyStreckenLogik();

        Assert.NotNull(entry.CodeMeta);
        Assert.Empty(entry.CodeMeta.Parameters);
        Assert.True(parameterNotifications > 0);
    }
}
