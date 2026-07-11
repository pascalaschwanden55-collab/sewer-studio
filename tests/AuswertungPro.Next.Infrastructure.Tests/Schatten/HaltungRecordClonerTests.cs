using System;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Schatten;

/// <summary>
/// Read-only-Fundament der Schattenauswertung: Die VSA-Bewertung mutiert den uebergebenen
/// Record (SetFieldValue). Der Kloner muss deshalb eine Kopie liefern, an der geschrieben
/// werden DARF (UserEdited-Sperre aufgehoben), ohne dass das Original irgendetwas davon sieht.
/// </summary>
public sealed class HaltungRecordClonerTests
{
    private static HaltungRecord Original()
    {
        var r = new HaltungRecord();
        r.SetFieldValue("Haltungsname", "10081-8993", FieldSource.Xtf, userEdited: false);
        r.SetFieldValue("DN_mm", "300", FieldSource.Xtf, userEdited: false);
        // Vom User editiertes Feld: die SetFieldValue-Sperre (HaltungRecord.cs:52) haengt daran.
        r.SetFieldValue("Zustandsklasse", "2", FieldSource.Manual, userEdited: true);
        r.VsaFindings.Add(new VsaFinding
        {
            KanalSchadencode = "BAB",
            Quantifizierung1 = "5",
            MeterStart = 12.4,
            EZD = 2
        });
        return r;
    }

    [Fact]
    public void KlonSchreiben_LaesstOriginalUnveraendert()
    {
        var original = Original();
        var modifiedBefore = original.ModifiedAtUtc;
        var propertyChangedAmOriginal = 0;
        original.PropertyChanged += (_, _) => propertyChangedAmOriginal++;

        var klon = HaltungRecordCloner.CloneForEvaluation(original);
        klon.SetFieldValue("VSA_Zustandsnote_D", "3.4", FieldSource.Manual, userEdited: false);
        klon.SetFieldValue("Zustandsklasse", "4", FieldSource.Manual, userEdited: false);

        Assert.Equal("", original.GetFieldValue("VSA_Zustandsnote_D"));
        Assert.Equal("2", original.GetFieldValue("Zustandsklasse"));
        Assert.Equal(modifiedBefore, original.ModifiedAtUtc);
        Assert.Equal(0, propertyChangedAmOriginal); // keine Binding-Stoerung im DataGrid
    }

    [Fact]
    public void UserEditedSperre_GiltAmKlonNicht_UndBleibtAmOriginalErhalten()
    {
        var original = Original();
        var klon = HaltungRecordCloner.CloneForEvaluation(original);

        // Am Original ist "Zustandsklasse" UserEdited=true -> Schreiben mit userEdited:false
        // wuerde dort verweigert. Am Klon MUSS es durchgehen, sonst blieben Mensch-Werte stehen.
        klon.SetFieldValue("Zustandsklasse", "4", FieldSource.Manual, userEdited: false);

        Assert.Equal("4", klon.GetFieldValue("Zustandsklasse"));
        Assert.True(original.FieldMeta["Zustandsklasse"].UserEdited); // Original-Meta ungeteilt
    }

    [Fact]
    public void KlonFindings_SindKopien_OriginalFindingBleibt()
    {
        var original = Original();
        var klon = HaltungRecordCloner.CloneForEvaluation(original);

        klon.VsaFindings[0].KanalSchadencode = "BBC";
        klon.VsaFindings[0].EZD = 4;
        klon.VsaFindings.Add(new VsaFinding { KanalSchadencode = "NEU" });

        Assert.Equal("BAB", original.VsaFindings[0].KanalSchadencode);
        Assert.Equal(2, original.VsaFindings[0].EZD);
        Assert.Single(original.VsaFindings);
    }
}
