using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Infrastructure.Ai.Training
{
    public static class TrainingCenterSettingsStore
    {
        private static string GetStorePath()
            => KnowledgeBasePaths.GetTrainingSettingsPath();

        public static async Task<TrainingCenterSettings> LoadAsync()
        {
            var path = GetStorePath();
            if (!File.Exists(path))
                return new TrainingCenterSettings();

            try
            {
                using var stream = File.OpenRead(path);
                var settings = await JsonSerializer.DeserializeAsync<TrainingCenterSettings>(stream);
                return settings ?? new TrainingCenterSettings();
            }
            catch
            {
                var backup = path + $".bad_{DateTime.UtcNow:yyyyMMddHHmmss}";
                File.Move(path, backup, overwrite: true);
                return new TrainingCenterSettings();
            }
        }

        public static async Task SaveAsync(TrainingCenterSettings settings)
        {
            var path = GetStorePath();
            using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, settings, Application.Common.JsonDefaults.Indented);
        }
    }
}
