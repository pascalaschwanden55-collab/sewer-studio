using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Charakterisierungstests für NvidiaSmiOutputParser (reine Textauswertung von nvidia-smi).
/// </summary>
public sealed class NvidiaSmiOutputParserTests
{
    // Typische nvidia-smi-Ausgabe (5 Pflichtfelder + Name):
    // utilization.gpu, memory.used, memory.total, temperature.gpu, clocks.current.graphics, name
    private const string TypischVollständig = "82, 4521, 12288, 65, 1920, NVIDIA GeForce RTX 4070";
    private const string TypischOhneNamen   = "10, 2048, 8192, 55, 1500";
    private const string MitNaPlatzhalter   = "5, 1000, 8192, [N/A], [N/A], NVIDIA GeForce RTX 3080";

    [Fact]
    public void Parse_VollständigeZeile_AlleFelder()
    {
        var r = NvidiaSmiOutputParser.Parse(TypischVollständig);
        Assert.NotNull(r);
        Assert.Equal(82, r!.GpuPercent);
        Assert.Equal(4521L, r.MemUsedMb);
        Assert.Equal(12288L, r.MemTotalMb);
        Assert.Equal(65, r.TempC);
        Assert.Equal(1920, r.ClockMhz);
        Assert.Equal("NVIDIA GeForce RTX 4070", r.GpuName);
    }

    [Fact]
    public void Parse_OhneNamen_NameLeer()
    {
        var r = NvidiaSmiOutputParser.Parse(TypischOhneNamen);
        Assert.NotNull(r);
        Assert.Equal(10, r!.GpuPercent);
        Assert.Equal(string.Empty, r.GpuName);
    }

    [Fact]
    public void Parse_NaPlatzhalterBeiTempUndTakt_OptionalFehlend()
    {
        var r = NvidiaSmiOutputParser.Parse(MitNaPlatzhalter);
        Assert.NotNull(r);
        Assert.Null(r!.TempC);
        Assert.Null(r.ClockMhz);
        Assert.Equal("NVIDIA GeForce RTX 3080", r.GpuName);
    }

    [Fact]
    public void Parse_LeeresString_NullResult()
    {
        Assert.Null(NvidiaSmiOutputParser.Parse(""));
        Assert.Null(NvidiaSmiOutputParser.Parse("   "));
    }

    [Fact]
    public void Parse_NullInput_NullResult()
    {
        Assert.Null(NvidiaSmiOutputParser.Parse(null!));
    }

    [Fact]
    public void Parse_ZuWenigFelder_NullResult()
    {
        Assert.Null(NvidiaSmiOutputParser.Parse("82, 4521, 12288, 65"));   // 4 statt 5
    }

    [Fact]
    public void Parse_UngültigerGpuProzent_NullResult()
    {
        Assert.Null(NvidiaSmiOutputParser.Parse("abc, 4521, 12288, 65, 1920"));
    }

    [Fact]
    public void Parse_FührendeUndNachfolgendeWhitespace_WirdIgnoriert()
    {
        var r = NvidiaSmiOutputParser.Parse("  5 , 100 , 8000 , 40 , 1200 , MyGPU  ");
        Assert.NotNull(r);
        Assert.Equal(5, r!.GpuPercent);
        Assert.Equal("MyGPU", r.GpuName);
    }

    [Fact]
    public void ComputeMemPercent_KorrektBeiTypischemWert()
    {
        // 4521 / 12288 ≈ 36.8 % → rundet auf 37
        var pct = NvidiaSmiOutputParser.ComputeMemPercent(4521, 12288);
        Assert.Equal(37, pct);
    }

    [Fact]
    public void ComputeMemPercent_NullGesamt_GibtNull()
    {
        Assert.Equal(0, NvidiaSmiOutputParser.ComputeMemPercent(100, 0));
    }

    [Fact]
    public void ComputeMemPercent_HundertProzent()
    {
        Assert.Equal(100, NvidiaSmiOutputParser.ComputeMemPercent(8192, 8192));
    }
}
