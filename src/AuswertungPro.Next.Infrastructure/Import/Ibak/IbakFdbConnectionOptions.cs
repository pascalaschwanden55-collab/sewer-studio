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
        var credentials = LoadCredentials();
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
    {
        var user = Environment.GetEnvironmentVariable(UserEnvVar);
        var password = Environment.GetEnvironmentVariable(PasswordEnvVar);
        return (
            string.IsNullOrWhiteSpace(user) ? DefaultUser : user.Trim(),
            string.IsNullOrWhiteSpace(password) ? DefaultPassword : password);
    }
}
