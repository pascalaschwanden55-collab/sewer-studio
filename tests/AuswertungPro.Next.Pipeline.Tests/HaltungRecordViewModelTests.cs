using System.Collections.Generic;
using System.ComponentModel;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Records;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Tests fuer ARCH-H1 Phase 1: HaltungRecordViewModel-Wrapper.
/// Stellt sicher dass der Wrapper das Domain-Modell korrekt spiegelt
/// und PropertyChanged 1:1 weiterleitet.
/// </summary>
public class HaltungRecordViewModelTests
{
    [Fact]
    public void Indexer_GetField_DelegatesToModel()
    {
        var model = new HaltungRecord();
        model.SetFieldValue("Haltungslaenge_m", "45.30", FieldSource.Manual, userEdited: true);

        using var vm = new HaltungRecordViewModel(model);
        Assert.Equal("45.30", vm["Haltungslaenge_m"]);
    }

    [Fact]
    public void SetFieldValue_PropagatesToModel()
    {
        var model = new HaltungRecord();
        using var vm = new HaltungRecordViewModel(model);

        vm.SetFieldValue("Material", "PE", FieldSource.Manual, userEdited: true);

        Assert.Equal("PE", model.GetFieldValue("Material"));
    }

    [Fact]
    public void SetFieldValue_RaisesIndexerPropertyChanged()
    {
        var model = new HaltungRecord();
        using var vm = new HaltungRecordViewModel(model);

        var raised = new List<string?>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.SetFieldValue("DN", "300", FieldSource.Manual, userEdited: true);

        // Domain-Modell selbst feuert PropertyChanged auf "Fields" + "Fields[DN]"
        // + "ModifiedAtUtc". Wrapper leitet das 1:1 weiter und feuert zusaetzlich
        // den indexer-typischen "Item[...]"-Event.
        Assert.Contains(raised, name => name == "Fields[DN]");
        Assert.Contains(raised, name => name == "ModifiedAtUtc");
    }

    [Fact]
    public void IdAndTimestamps_DelegateToModel()
    {
        var model = new HaltungRecord();
        using var vm = new HaltungRecordViewModel(model);

        Assert.Equal(model.Id, vm.Id);
        Assert.Equal(model.CreatedAtUtc, vm.CreatedAtUtc);
        Assert.Equal(model.ModifiedAtUtc, vm.ModifiedAtUtc);
    }

    [Fact]
    public void Dispose_UnhooksFromModelEvents()
    {
        var model = new HaltungRecord();
        var vm = new HaltungRecordViewModel(model);

        var raisedAfterDispose = false;
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, _) => raisedAfterDispose = true;

        vm.Dispose();

        // Nach Dispose ist der Event-Handler abgemeldet — Modell-Events
        // erreichen das ViewModel nicht mehr.
        model.SetFieldValue("Material", "Beton", FieldSource.Manual, userEdited: true);
        Assert.False(raisedAfterDispose, "PropertyChanged sollte nach Dispose nicht mehr feuern");
    }

    [Fact]
    public void UserEditedField_NotOverwrittenByImport()
    {
        // HaltungRecord.SetFieldValue protected user-edited values gegen
        // Import-Overwrites. ViewModel muss dieses Verhalten transparent
        // weiterreichen.
        var model = new HaltungRecord();
        using var vm = new HaltungRecordViewModel(model);

        vm.SetFieldValue("Material", "Manuell-PE", FieldSource.Manual, userEdited: true);
        // Import-Versuch (userEdited=false) muss blockiert werden
        vm.SetFieldValue("Material", "Import-Beton", FieldSource.Xtf, userEdited: false);

        Assert.Equal("Manuell-PE", vm["Material"]);
    }
}
