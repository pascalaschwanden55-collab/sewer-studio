using System;
using System.Linq;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Sichert die Verteil-Karten-Vorschau: Normal endet an der PDF, Sanierung zeigt
/// zusaetzlich die Ebene {Datum}_{Objekt}_Saniert {Jahr} in Pfad und Ordnerbaum.
/// </summary>
public sealed class DistributionTargetVariantPreviewTests
{
    private static DistributionTargetConfigViewModel BuildSchachtTarget()
    {
        var cfg = new DistributionTargetConfig();
        var resolver = new DistributionPatternResolver();
        var sample = new DistributionPatternContext(new DateTime(2026, 7, 15), "Altdorf", Schachtnummer: "80454");
        var vm = new DistributionTargetConfigViewModel(
            "Schächte verteilen", "Schachtprotokoll je Schacht",
            cfg, resolver, sample, ".pdf",
            showFilePattern: false, "hinweis", () => { }, () => null,
            fixedPattern: "{Datum}_{Schachtnummer}",
            fixedObjectFolderPattern: "{Schachtnummer}",
            supportsSanierung: true);
        vm.Root = @"C:\Ziel";
        return vm;
    }

    [Fact]
    public void Normal_Vorschau_endet_an_der_Pdf_ohne_Saniert()
    {
        var vm = BuildSchachtTarget();
        vm.PreviewVariant = DistributionVariant.Normal;

        Assert.DoesNotContain("_Saniert", vm.Vorschau);
        Assert.EndsWith(@"80454\20260715_80454.pdf", vm.Vorschau);
        Assert.DoesNotContain(vm.TreeNodes, n => n.Label.Contains("_Saniert"));
    }

    [Fact]
    public void Sanierung_Vorschau_und_Baum_zeigen_Saniert_Ebene()
    {
        var vm = BuildSchachtTarget();
        vm.PreviewVariant = DistributionVariant.Sanierung;

        Assert.Contains(@"20260715_80454_Saniert 2026", vm.Vorschau);
        Assert.Contains(vm.TreeNodes, n =>
            n.Kind == DistributionTreeNodeKind.Ordner && n.Label.Contains("_Saniert 2026"));
        Assert.Contains(vm.TreeNodes, n => n.Kind == DistributionTreeNodeKind.Pdf);
    }
}
