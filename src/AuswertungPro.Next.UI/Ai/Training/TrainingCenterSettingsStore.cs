using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Ai.Training
{
    public static class TrainingCenterSettingsStore
    {
        private static string GetStorePath()
            => KnowledgeRoot.GetTrainingSettingsPath();

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
            // B8 Fix: Atomic Write — Temp-Datei schreiben, dann umbenennen.
            // Verhindert korrupte Settings bei Crash waehrend Serialize.
            var path = GetStorePath();
            var dir = Path.GetDirectoryName(path);
            if (dir != null) Directory.CreateDirectory(dir);
            var tmp = path + ".tmp";
            using (var stream = File.Create(tmp))
            {
                await JsonSerializer.SerializeAsync(stream, settings, Application.Common.JsonDefaults.Indented);
            }
            File.Move(tmp, path, overwrite: true);
        }
    }
}
