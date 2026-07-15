using System;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.UI.ViewModels.Windows;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ReviewCardViewModelTests
{
    private static ReviewQueueItem Item(string matchLevel, string protocolCode, string? kiCode)
        => new("id", null, 0.9, DateTime.UtcNow)
        {
            SelfTrainingCaseId = "06.1-2",
            SelfTrainingVsaCode = protocolCode,
            SelfTrainingSuggestedCode = kiCode,
            SelfTrainingMeter = 12.3,
            SelfTrainingFramePath = "f.png",
            SelfTrainingMatchLevel = matchLevel,
            SelfTrainingSampleId = "06.1-2_st_001"
        };

    [Fact]
    public void NoFindings_zeigt_nichts_erkannt_und_ist_KI_Fehler()
    {
        var vm = new ReviewCardViewModel(Item("NoFindings", "BAB", null));
        Assert.Equal("nichts erkannt", vm.KiAussage);
        Assert.True(vm.IsNoFindings);
        Assert.True(vm.IsKiError);
        Assert.Equal("BAB", vm.ProtocolCode);
        Assert.Equal(12.3, vm.Meter);
        Assert.Equal("f.png", vm.FramePath);
        Assert.Equal("06.1-2_st_001", vm.SampleId);
    }

    [Fact]
    public void Mismatch_zeigt_KI_Code_und_ist_KI_Fehler()
    {
        var vm = new ReviewCardViewModel(Item("Mismatch", "BAB", "BAC"));
        Assert.Equal("BAC", vm.KiAussage);   // KI-Code (vs. ProtocolCode BAB)
        Assert.Equal("BAB", vm.ProtocolCode);
        Assert.False(vm.IsNoFindings);
        Assert.True(vm.IsKiError);
    }

    [Fact]
    public void ExactMatch_ist_kein_KI_Fehler()
    {
        var vm = new ReviewCardViewModel(Item("ExactMatch", "BAB", "BAB"));
        Assert.False(vm.IsKiError);
        Assert.False(vm.IsNoFindings);
    }

    [Fact]
    public void Fehlender_KI_Code_wird_verstaendlich_beschriftet()
    {
        var vm = new ReviewCardViewModel(Item("Mismatch", "BAB", null));

        Assert.Equal("Unbekannt", vm.KiAussage);
    }

    [Fact]
    public void ProtocolStartdata_zeigt_Strich_und_Startdaten_Label()
    {
        // Protokoll-Startdaten: kein KI-Durchlauf, Code identisch (SelfTrainingSuggestedCode = ProtocolCode)
        var vm = new ReviewCardViewModel(Item("ProtocolStartdata", "BAB", "BAB"));
        Assert.Equal("—", vm.KiAussage);
        Assert.Equal("Protokoll-Startdaten", vm.StatusLabel);
        Assert.False(vm.IsKiError);
        Assert.True(vm.IsProtocolStartdata);
    }
}
