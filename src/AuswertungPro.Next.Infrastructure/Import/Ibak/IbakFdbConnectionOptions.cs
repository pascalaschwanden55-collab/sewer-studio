using FirebirdSql.Data.FirebirdClient;

namespace AuswertungPro.Next.Infrastructure.Import.Ibak;

public static class IbakFdbConnectionOptions
{
    public const string UserEnvVar = "IBAK_FDB_USER";
    public const string PasswordEnvVar = "IBAK_FDB_PASSWORD";
    public const string DefaultUser = "SYSDBA";
    public const string DefaultPassword = "masterkey";

    public static FbConnectionStringBuilder CreateEmbedded(string databasePath, string charset = "NONE")
    {
        var credentials = LoadCredentials();
        return new FbConnectionStringBuilder
        {
            Database = databasePath,
            UserID = credentials.User,
            Password = credentials.Password,
            ServerType = FbServerType.Embedded,
            Charset = charset
        };
    }

    public static FbConnectionStringBuilder CreatePhotoMap(string databasePath, string? clientLibrary)
    {
        var credentials = LoadCredentials(requireExplicit: IsServerDatabasePath(databasePath));
        var builder = new FbConnectionStringBuilder
        {
            Database = databasePath,
            UserID = credentials.User,
            Password = credentials.Password,
            Charset = "WIN1252",
            Dialect = 3,
            Pooling = false
        };

        if (!string.IsNullOrWhiteSpace(clientLibrary))
            builder.ClientLibrary = clientLibrary;

        return builder;
    }

    public static (string User, string Password) LoadCredentials()
        => LoadCredentials(requireExplicit: false);

    private static (string User, string Password) LoadCredentials(bool requireExplicit)
    {
        var user = Environment.GetEnvironmentVariable(UserEnvVar);
        var password = Environment.GetEnvironmentVariable(PasswordEnvVar);

        if (requireExplicit
            && (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password)))
        {
            throw new InvalidOperationException(
                "Firebird-Serverzugriff benoetigt ausdrueckliche Zugangsdaten. " +
                $"Setze {UserEnvVar} und {PasswordEnvVar}; die lokalen Embedded-Standardwerte " +
                "werden fuer Serverpfade nicht verwendet.");
        }

        return (
            string.IsNullOrWhiteSpace(user) ? DefaultUser : user.Trim(),
            string.IsNullOrWhiteSpace(password) ? DefaultPassword : password);
    }

    internal static bool IsServerDatabasePath(string? databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            return false;

        var path = databasePath.Trim();
        if (path.StartsWith(@"\\", StringComparison.Ordinal)
            || path.Contains("://", StringComparison.Ordinal)
            || (path.StartsWith("[", StringComparison.Ordinal) && path.Contains("]:", StringComparison.Ordinal)))
        {
            return true;
        }

        var firstColon = path.IndexOf(':');
        return firstColon > 1;
    }
}
