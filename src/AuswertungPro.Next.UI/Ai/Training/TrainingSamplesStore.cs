using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Ai.Training
{
    public static class TrainingSamplesStore
    {
        private static string GetStorePath() => KnowledgeRoot.GetTrainingSamplesPath();

        public static async Task<List<TrainingSample>> LoadAsync()
        {
            var path = GetStorePath();
            if (!File.Exists(path))
                return new List<TrainingSample>();

            try
            {
                using var stream = File.OpenRead(path);
                var samples = await JsonSerializer.DeserializeAsync<List<TrainingSample>>(stream);
                return samples ?? new List<TrainingSample>();
            }
            catch
            {
                var backup = path + ".bad";
                File.Move(path, backup, overwrite: true);
                return new List<TrainingSample>();
            }
        }

        public static async Task SaveAsync(List<TrainingSample> samples)
        {
            var path = GetStorePath();
            using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, samples, Application.Common.JsonDefaults.Indented);
        }
    }
}
