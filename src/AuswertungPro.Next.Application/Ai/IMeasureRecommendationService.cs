using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Ai;

public interface IMeasureRecommendationService
{
    MeasureRecommendationResult Recommend(HaltungRecord record, int maxSuggestions = 5);
    MeasureLearningStats GetStats();
    MeasureModelTrainingResult TrainModel(int minSamples = 25);
    bool Learn(HaltungRecord record);
}
