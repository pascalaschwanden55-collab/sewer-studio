using System;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Die Sicherungskopie vor einer Bearbeitung. Ein halbtiefer Klon wuerde die
/// Zeilenlisten teilen — ein Ruecksetzen waere dann wirkungslos, und der
/// Bildschirm zeigte Angaben, die nicht auf der Platte stehen.
/// </summary>
public sealed class DossierDeepCopyTests
{
    [Fact]
    public void Die_Kopie_teilt_keine_Listen_mit_dem_Original()
    {
        var original = new DossierDefinition { Name = "Musterweg 1" };
        original.Owners.Add(new DossierOwnerRow { Name = "Dittli" });
        original.Topics.Add(new DossierTopicRow { Title = "Schäden", Text = "alt" });
        original.HoldingIds.Add(Guid.NewGuid());
        original.ShaftNumbers.Add("80551");
        original.TocAttachments.Add(new DossierTocAttachment
        {
            Title = "Protokolle",
            PageNumber = "8"
        });

        var kopie = DossierDeepCopy.Of(original);

        original.Owners[0].Name = "geändert";
        original.Topics[0].Text = "geändert";
        original.HoldingIds.Clear();
        original.ShaftNumbers.Clear();
        original.TocAttachments[0].PageNumber = "12";
        original.Name = "geändert";

        Assert.Equal("Musterweg 1", kopie.Name);
        Assert.Equal("Dittli", kopie.Owners[0].Name);
        Assert.Equal("alt", kopie.Topics[0].Text);
        Assert.Single(kopie.HoldingIds);
        Assert.Equal(new[] { "80551" }, kopie.ShaftNumbers);
        Assert.Equal("Protokolle", kopie.TocAttachments[0].Title);
        Assert.Equal("8", kopie.TocAttachments[0].PageNumber);
    }

    [Fact]
    public void Auch_verschachtelte_Formatierungen_werden_kopiert()
    {
        var original = new DossierDefinition();
        original.FieldStyles["Text"] = new()
        {
            new DossierTextStyleRange { Start = 0, Length = 3, Bold = true }
        };

        var kopie = DossierDeepCopy.Of(original);
        original.FieldStyles["Text"][0].Bold = false;

        Assert.True(kopie.FieldStyles["Text"][0].Bold);
    }

    [Fact]
    public void Auch_die_Gebietsangaben_lassen_sich_kopieren()
    {
        var original = new DossierAreaSettings { AreaTitle = "Erstfeld West" };
        original.Topics.Add(new DossierTopicRow { Title = "Beilagen", Text = "alt" });

        var kopie = DossierDeepCopy.Of(original);
        original.Topics[0].Text = "geändert";
        original.AreaTitle = "geändert";

        Assert.Equal("Erstfeld West", kopie.AreaTitle);
        Assert.Equal("alt", kopie.Topics[0].Text);
    }

    [Fact]
    public void Die_Kennung_bleibt_erhalten()
    {
        // Ohne sie fände das Ruecksetzen das Dossier nicht wieder.
        var original = new DossierDefinition();

        Assert.Equal(original.Id, DossierDeepCopy.Of(original).Id);
    }

    [Fact]
    public void Ohne_Vorlage_gibt_es_keine_Kopie()
        => Assert.Throws<ArgumentNullException>(
            () => DossierDeepCopy.Of<DossierDefinition>(null!));
}
