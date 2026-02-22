using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Ai.Training
{
    public static class TrainingCenterSettingsStore
    {
        private static string GetStorePath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "AuswertungPro");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "training_center_settings.json");
        }

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
            catch (Exception ex)
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
            var opts = new JsonSerializerOptions { WriteIndented = true };
            await JsonSerializer.SerializeAsync(stream, settings, opts);
        }
    }
}
