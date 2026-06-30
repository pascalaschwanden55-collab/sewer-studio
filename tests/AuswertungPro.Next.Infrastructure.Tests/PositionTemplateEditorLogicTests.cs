using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer die aus PositionTemplateEditorViewModel
/// extrahierte reine Geschaeftslogik (PositionTemplateCopier und PositionListEditor).
/// </summary>
public sealed class PositionTemplateEditorLogicTests
{
    // ---------------------------------------------------------------------------
    // PositionTemplateCopier
    // ---------------------------------------------------------------------------

    [Fact]
    public void DeepCopyTemplate_KopiertAlleFelder()
    {
        var original = new PositionTemplate
        {
            ItemKey = "KEY1",
            Enabled = true,
            DefaultQty = 3.5m,
            Name = "Rohrreinigung",
            Unit = "m",
            Price = 42.50m,
            IsCustom = false
        };

        var copy = PositionTemplateCopier.DeepCopy(original);

        Assert.Equal(original.ItemKey, copy.ItemKey);
        Assert.Equal(original.Enabled, copy.Enabled);
        Assert.Equal(original.DefaultQty, copy.DefaultQty);
        Assert.Equal(original.Name, copy.Name);
        Assert.Equal(original.Unit, copy.Unit);
        Assert.Equal(original.Price, copy.Price);
        Assert.Equal(original.IsCustom, copy.IsCustom);
    }

    [Fact]
    public void DeepCopyTemplate_IstEchteKopie_AenderungAufOriginalBeeinflusstKopieNicht()
    {
        var original = new PositionTemplate { Name = "Alt", Price = 10m };
        var copy = PositionTemplateCopier.DeepCopy(original);

        // Original nachtraeglich aendern – Kopie darf unbeeinflusst bleiben
        original = original with { Name = "Neu", Price = 99m };

        Assert.Equal("Alt", copy.Name);
        Assert.Equal(10m, copy.Price);
    }

    [Fact]
    public void DeepCopyGroup_KopiertNameUndPositionen()
    {
        var group = new PositionGroup
        {
            Name = "Gruppe A",
            Positions = new List<PositionTemplate>
            {
                new() { Name = "P1", Price = 1m },
                new() { Name = "P2", Price = 2m }
            }
        };

        var copy = PositionTemplateCopier.DeepCopy(group);

        Assert.Equal("Gruppe A", copy.Name);
        Assert.Equal(2, copy.Positions.Count);
        Assert.Equal("P1", copy.Positions[0].Name);
        Assert.Equal("P2", copy.Positions[1].Name);
        // Unterschiedliche Listen-Instanzen
        Assert.NotSame(group.Positions, copy.Positions);
    }

    [Fact]
    public void DeepCopyAll_KopiertAlleGruppen()
    {
        var groups = new List<PositionGroup>
        {
            new() { Name = "G1", Positions = new List<PositionTemplate> { new() { Name = "P1" } } },
            new() { Name = "G2", Positions = new List<PositionTemplate>() }
        };

        var copies = PositionTemplateCopier.DeepCopyAll(groups).ToList();

        Assert.Equal(2, copies.Count);
        Assert.Equal("G1", copies[0].Name);
        Assert.Equal("G2", copies[1].Name);
        Assert.Equal(1, copies[0].Positions.Count);
    }

    // ---------------------------------------------------------------------------
    // PositionListEditor – CanMoveUp / CanMoveDown
    // ---------------------------------------------------------------------------

    [Fact]
    public void CanMoveUp_ErstesElement_IstFalse()
    {
        var list = MakeList("A", "B", "C");
        Assert.False(PositionListEditor.CanMoveUp(list, 0));
    }

    [Fact]
    public void CanMoveUp_NichtErstesElement_IstTrue()
    {
        var list = MakeList("A", "B", "C");
        Assert.True(PositionListEditor.CanMoveUp(list, 1));
        Assert.True(PositionListEditor.CanMoveUp(list, 2));
    }

    [Fact]
    public void CanMoveDown_LetztesElement_IstFalse()
    {
        var list = MakeList("A", "B", "C");
        Assert.False(PositionListEditor.CanMoveDown(list, 2));
    }

    [Fact]
    public void CanMoveDown_NichtLetztesElement_IstTrue()
    {
        var list = MakeList("A", "B", "C");
        Assert.True(PositionListEditor.CanMoveDown(list, 0));
        Assert.True(PositionListEditor.CanMoveDown(list, 1));
    }

    [Fact]
    public void CanMoveDown_NegativerIndex_IstFalse()
    {
        var list = MakeList("A");
        Assert.False(PositionListEditor.CanMoveDown(list, -1));
    }

    // ---------------------------------------------------------------------------
    // PositionListEditor – MoveUp / MoveDown
    // ---------------------------------------------------------------------------

    [Fact]
    public void MoveUp_TauschtElementMitVorgaenger()
    {
        var list = MakeList("A", "B", "C");

        var moved = PositionListEditor.MoveUp(list, 1);

        Assert.True(moved);
        Assert.Equal("B", list[0].Name);
        Assert.Equal("A", list[1].Name);
        Assert.Equal("C", list[2].Name);
    }

    [Fact]
    public void MoveUp_ErstesElement_BewirktNichts()
    {
        var list = MakeList("A", "B");

        var moved = PositionListEditor.MoveUp(list, 0);

        Assert.False(moved);
        Assert.Equal("A", list[0].Name);
        Assert.Equal("B", list[1].Name);
    }

    [Fact]
    public void MoveDown_TauschtElementMitNachfolger()
    {
        var list = MakeList("A", "B", "C");

        var moved = PositionListEditor.MoveDown(list, 1);

        Assert.True(moved);
        Assert.Equal("A", list[0].Name);
        Assert.Equal("C", list[1].Name);
        Assert.Equal("B", list[2].Name);
    }

    [Fact]
    public void MoveDown_LetztesElement_BewirktNichts()
    {
        var list = MakeList("A", "B");

        var moved = PositionListEditor.MoveDown(list, 1);

        Assert.False(moved);
        Assert.Equal("A", list[0].Name);
        Assert.Equal("B", list[1].Name);
    }

    // ---------------------------------------------------------------------------
    // PositionListEditor – RemoveAndGetNextIndex
    // ---------------------------------------------------------------------------

    [Fact]
    public void RemoveAndGetNextIndex_MittleresElement_WaehltGleichenIndex()
    {
        var list = MakeList("A", "B", "C");

        var nextIndex = PositionListEditor.RemoveAndGetNextIndex(list, 1);

        // B entfernt, Liste = [A, C], naechster Index = 1 (= C)
        Assert.Equal(1, nextIndex);
        Assert.Equal(2, list.Count);
        Assert.Equal("A", list[0].Name);
        Assert.Equal("C", list[1].Name);
    }

    [Fact]
    public void RemoveAndGetNextIndex_LetztesElement_WaehltVorgaenger()
    {
        var list = MakeList("A", "B", "C");

        var nextIndex = PositionListEditor.RemoveAndGetNextIndex(list, 2);

        // C entfernt, Liste = [A, B], naechster Index = 1 (= B, unveraendert klemmt auf Count-1)
        Assert.Equal(1, nextIndex);
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void RemoveAndGetNextIndex_EinzigesElement_GibtMinusEinZurueck()
    {
        var list = MakeList("A");

        var nextIndex = PositionListEditor.RemoveAndGetNextIndex(list, 0);

        Assert.Equal(-1, nextIndex);
        Assert.Empty(list);
    }

    // ---------------------------------------------------------------------------
    // PositionListEditor – CreateDefault
    // ---------------------------------------------------------------------------

    [Fact]
    public void CreateDefault_LiefertPositionMitStandardwerten()
    {
        var pos = PositionListEditor.CreateDefault();

        Assert.True(pos.Enabled);
        Assert.Equal(1m, pos.DefaultQty);
        Assert.Equal("Neue Position", pos.Name);
        Assert.Equal("Stk", pos.Unit);
        Assert.Equal(0m, pos.Price);
        Assert.True(pos.IsCustom);
    }

    [Fact]
    public void CreateDefault_GibtJedesmalNeueInstanz()
    {
        var a = PositionListEditor.CreateDefault();
        var b = PositionListEditor.CreateDefault();
        Assert.NotSame(a, b);
    }

    // ---------------------------------------------------------------------------
    // Hilfsmethode
    // ---------------------------------------------------------------------------

    private static List<PositionTemplate> MakeList(params string[] names) =>
        names.Select(n => new PositionTemplate { Name = n }).ToList();
}
