using System;
using System.IO;

namespace AuswertungPro.Next.Application.Common;

public static class AppDataPathResolver
{
    public const string AppDataDirEnvVar = "SEWERSTUDIO_APPDATA_DIR";
    public const string DefaultProductName = "SewerStudio";

    public static string Resolve(string productName = DefaultProductName)
    {
        var overridePath = Environment.GetEnvironmentVariable(AppDataDirEnvVar);
        return string.IsNullOrWhiteSpace(overridePath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), productName)
            : overridePath;
    }
}
