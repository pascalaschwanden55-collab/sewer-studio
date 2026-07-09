using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// ViewModel des einfachen Schacht-Sanierungsmassnahmen-Fensters: klickbare Liste ->
/// gewaehlte Positionen (Menge/Preis pro Schacht) -> Uebernehmen schreibt Record + meldet Auswahl.
/// </summary>
public sealed class SchachtMassnahmenViewModelTests
{
    private static SchachtRecord Record(string nr = "KS 1", string funktion = "Normschacht", string zk = "3")
    {
        var r = new SchachtRecord();
        r.SetFieldValue("Schachtnummer", nr);
        r.SetFieldValue("Funktion", funktion);
        r.SetFieldValue("Zustandsklasse", zk);
        return r;
    }

    private static SchachtMassnahmeKatalogEintrag E(string name, decimal preis) => new() { Name = name, Preis = preis };

    private static SchachtMassnahmenViewModel Vm(SchachtRecord record, params SchachtMassnahmeKatalogEintrag[] katalog)
        => new(record, katalog, null, _ => { });

    [Fact]
    public void Hinzufuegen_legt_Position_mit_Menge1_und_Listenpreis_an()
    {
        var vm = Vm(Record(), E("Deckel", 450m));

        vm.HinzufuegenCommand.Execute(E("Deckel", 450m));

        var pos = Assert.Single(vm.Positionen);
        Assert.Equal("Deckel", pos.Name);
        Assert.Equal(1m, pos.Menge);
        Assert.Equal(450m, pos.Preis);
        Assert.Equal(450m, vm.Total);
    }

    [Fact]
    public void Uebernehmen_RahmenUndDeckel_setzt_AbdeckungStk_auf_1()
    {
        var record = Record();
        var vm = Vm(record, E("Rahmen und Deckel ersetzen", 850m));

        vm.HinzufuegenCommand.Execute(E("Rahmen und Deckel ersetzen", 850m));
        vm.UebernehmenCommand.Execute(null);

        Assert.Equal("1", record.GetFieldValue("Abdeckung Stk."));
    }

    [Fact]
    public void Uebernehmen_RahmenUndDeckel_ueberschreibt_bestehende_AbdeckungStk_nicht()
    {
        var record = Record();
        record.SetFieldValue("Abdeckung Stk.", "2");
        var vm = Vm(record, E("Rahmen und Deckel ersetzen", 850m));

        vm.HinzufuegenCommand.Execute(E("Rahmen und Deckel ersetzen", 850m));
        vm.UebernehmenCommand.Execute(null);

        Assert.Equal("2", record.GetFieldValue("Abdeckung Stk."));
    }

    [Fact]
    public void Hinzufuegen_gleicher_Name_erhoeht_Menge_statt_Duplikat()
    {
        var vm = Vm(Record(), E("Deckel", 450m));

        vm.HinzufuegenCommand.Execute(E("Deckel", 450m));
        vm.HinzufuegenCommand.Execute(E("Deckel", 450m));

        var pos = Assert.Single(vm.Positionen);
        Assert.Equal(2m, pos.Menge);
        Assert.Equal(900m, vm.Total);
    }

    [Fact]
    public void Preisaenderung_an_Position_aktualisiert_Total()
    {
        var vm = Vm(Record(), E("Deckel", 450m));
        vm.HinzufuegenCommand.Execute(E("Deckel", 450m));

        vm.Positionen[0].Preis = 500m;

        Assert.Equal(500m, vm.Total);
    }

    [Fact]
    public void Entfernen_loescht_Position_und_aktualisiert_Total()
    {
        var vm = Vm(Record(), E("Deckel", 450m));
        vm.HinzufuegenCommand.Execute(E("Deckel", 450m));

        vm.EntfernenCommand.Execute(vm.Positionen[0]);

        Assert.Empty(vm.Positionen);
        Assert.Equal(0m, vm.Total);
    }

    [Fact]
    public void Uebernehmen_schreibt_Record_ruft_Callback_und_schliesst()
    {
        var record = Record();
        HoldingCost? captured = null;
        var closed = false;
        var vm = new SchachtMassnahmenViewModel(record, new[] { E("Deckel", 450m), E("Fugen", 220m) }, null, c => captured = c);
        vm.CloseRequested += () => closed = true;

        vm.HinzufuegenCommand.Execute(E("Deckel", 450m));
        vm.HinzufuegenCommand.Execute(E("Fugen", 220m));
        vm.UebernehmenCommand.Execute(null);

        Assert.Equal("Deckel; Fugen", record.GetFieldValue("Massnahmen"));
        Assert.Equal("670.00", record.GetFieldValue("Kosten"));
        Assert.NotNull(captured);
        Assert.Equal(670m, captured!.Total);
        Assert.True(closed);
    }

    [Fact]
    public void Uebernehmen_ohne_Positionen_leert_Record_Felder()
    {
        var record = Record();
        record.SetFieldValue("Massnahmen", "Alt");
        record.SetFieldValue("Kosten", "999.00");
        var vm = new SchachtMassnahmenViewModel(record, Array.Empty<SchachtMassnahmeKatalogEintrag>(), null, _ => { });

        vm.UebernehmenCommand.Execute(null);

        Assert.Equal("", record.GetFieldValue("Massnahmen"));
        Assert.Equal("", record.GetFieldValue("Kosten"));
    }

    [Fact]
    public void Ctor_uebernimmt_bestehende_Auswahl()
    {
        var measure = new MeasureCost();
        measure.Lines.Add(new CostLine { Text = "Deckel", Qty = 2m, UnitPrice = 450m, Selected = true });
        var bestehend = new HoldingCost { Holding = "KS 1", Measures = { measure } };

        var vm = new SchachtMassnahmenViewModel(Record(), new[] { E("Deckel", 450m) }, bestehend, _ => { });

        var pos = Assert.Single(vm.Positionen);
        Assert.Equal("Deckel", pos.Name);
        Assert.Equal(2m, pos.Menge);
        Assert.Equal(900m, vm.Total);
    }

    [Fact]
    public void ListeBearbeiten_ersetzt_Katalog_mit_Ergebnis()
    {
        IReadOnlyList<SchachtMassnahmeKatalogEintrag> neu = new[] { E("Neu A", 10m), E("Neu B", 20m) };
        var vm = new SchachtMassnahmenViewModel(Record(), new[] { E("Alt", 5m) }, null, _ => { }, () => neu);

        vm.ListeBearbeitenCommand.Execute(null);

        Assert.Equal(2, vm.Katalog.Count);
        Assert.Equal("Neu A", vm.Katalog[0].Name);
    }
}
