using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Ai.Training
{
    public static class TrainingSamplesStore
    {
        private static string GetStorePath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "AuswertungPro");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "training_center_samples.json");
        }

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
            var opts = new JsonSerializerOptions { WriteIndented = true };
            await JsonSerializer.SerializeAsync(stream, samples, opts);
        }
    }
}
