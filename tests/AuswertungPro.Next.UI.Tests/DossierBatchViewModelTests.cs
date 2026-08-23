using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.UI.ViewModels.Windows;

using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DossierBatchViewModelTests
{
    private static DossierProposal Vorschlag(string nummer, bool waehlbar, string grund = "")
    {
        var parzelle = new ParcelInfo(nummer, 1206, "Musterdorf", 500, "CH1",
            "POLYGON((0 0,1 0,1 1,0 0))", "https://example.invalid/gb");

        var eintrag = new LandRegistryEntry("Musterstrasse", "30", "6472", "Musterdorf",
            new[] { new LandRegistryOwner("", "Martin Muster", "Musterstrasse 30, 6472 Musterdorf", "") },
            NoOwnerRegistered: false);

        var leitungen = new[]
        {
            new ProposedHolding("36051-36329", true, true, true, "Lage"),
            new ProposedHolding("36329-35558", false, true, false, "Lage")
        };

        return new DossierProposal(parzelle, eintrag, leitungen,
            "Liegenschaft Nr. " + nummer + " Muster", waehlbar, grund);
    }

    [Fact]
    public void Waehlbare_Vorschlaege_sind_angehakt_gesperrte_nicht()
    {
        var vm = new DossierBatchViewModel();
        vm.Uebernehmen(new DossierBatchProposalResult(
            new[] { Vorschlag("439", true), Vorschlag("13", false, "kein Eigentümer") },
            Array.Empty<string>()));

        Assert.True(vm.Rows[0].IsSelected);
        Assert.False(vm.Rows[1].IsSelected);
        Assert.False(vm.Rows[1].CanSelect);
        Assert.Equal(1, vm.SelectedCount);
    }

    [Fact]
    public void Ein_gesperrter_Vorschlag_laesst_sich_nicht_anhaken()
    {
        var vm = new DossierBatchViewModel();
        vm.Uebernehmen(new DossierBatchProposalResult(
            new[] { Vorschlag("13", false, "kein Eigentümer") }, Array.Empty<string>()));

        vm.Rows[0].IsSelected = true;

        Assert.False(vm.Rows[0].IsSelected);
        Assert.Equal(0, vm.SelectedCount);
    }

    [Fact]
    public void Die_Auswahl_reicht_nur_angehakte_Leitungen_weiter()
    {
        var vm = new DossierBatchViewModel();
        vm.Uebernehmen(new DossierBatchProposalResult(
            new[] { Vorschlag("439", true) }, Array.Empty<string>()));

        var auswahl = vm.BaueAuswahl();

        var eintrag = Assert.Single(auswahl);
        Assert.Equal(new[] { "36051-36329" }, eintrag.SelectedHoldingDesignations);
    }

    [Fact]
    public void Warnungen_werden_sichtbar_gemacht()
    {
        var vm = new DossierBatchViewModel();
        vm.Uebernehmen(new DossierBatchProposalResult(
            Array.Empty<DossierProposal>(), new[] { "Dienst nicht erreichbar" }));

        Assert.Contains("Dienst nicht erreichbar", vm.WarningText, StringComparison.Ordinal);
    }
}
