using AuswertungPro.Next.Domain.Models;
using Xunit;

public sealed class ModernizerRecordFieldSpecsTests
{
    [Fact]
    public void HaltungPathFieldsKeepExpectedOrderAndListMarker()
    {
        Assert.Equal(
            new[]
            {
                FieldKeys.Link,
                ModernizerProjectKeys.SecondaryVideoLink,
                FieldKeys.PdfPath,
                FieldKeys.PdfEigen,
                FieldKeys.PdfAll
            },
            ModernizerRecordFieldSpecs.HaltungPathFields.Select(spec => spec.Field));
        Assert.Equal(
            new[] { false, false, false, false, true },
            ModernizerRecordFieldSpecs.HaltungPathFields.Select(spec => spec.IsList));
    }

    [Fact]
    public void HaltungDateStampFieldsKeepExpectedFallbackOrder()
    {
        Assert.Equal(
            new[]
            {
                FieldKeys.PdfPath,
                FieldKeys.PdfEigen,
                FieldKeys.Link,
                ModernizerProjectKeys.SecondaryVideoLink
            },
            ModernizerRecordFieldSpecs.HaltungDateStampFields.Select(spec => spec.Field));
    }

    [Fact]
    public void SchachtPathFieldsKeepExpectedOrder()
    {
        Assert.Equal(
            new[] { FieldKeys.Link, FieldKeys.PdfPath },
            ModernizerRecordFieldSpecs.SchachtPathFields.Select(spec => spec.Field));
    }
}
