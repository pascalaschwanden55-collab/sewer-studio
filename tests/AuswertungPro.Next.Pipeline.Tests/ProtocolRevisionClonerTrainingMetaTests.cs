using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ProtocolRevisionClonerTrainingMetaTests
{
    [Fact]
    public void CloneEntry_copies_training_meta_and_sample_ids_independently()
    {
        var source = new ProtocolEntry
        {
            Training = new ProtocolEntryTrainingMeta
            {
                SkipAutomaticPersistence = true,
                SkipReason = "Fotoannotation bereits separat gespeichert",
                PhotoAnnotationSampleIds = ["photo-sample-1"]
            }
        };

        var clone = ProtocolRevisionCloner.CloneEntry(source);

        Assert.NotNull(clone.Training);
        Assert.NotSame(source.Training, clone.Training);
        Assert.True(clone.Training!.SkipAutomaticPersistence);
        Assert.Equal("Fotoannotation bereits separat gespeichert", clone.Training.SkipReason);
        Assert.NotSame(source.Training.PhotoAnnotationSampleIds, clone.Training.PhotoAnnotationSampleIds);
        Assert.Equal(["photo-sample-1"], clone.Training.PhotoAnnotationSampleIds);

        source.Training.PhotoAnnotationSampleIds.Add("photo-sample-2");
        Assert.Single(clone.Training.PhotoAnnotationSampleIds);
    }
}
