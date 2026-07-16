using FirebirdSql.Data.FirebirdClient;

namespace AuswertungPro.Next.Infrastructure.Import.Ibak;

public interface IIbakFdbConnectionOptions
{
    FbConnectionStringBuilder CreateEmbedded(string databasePath, string charset = "NONE");

    FbConnectionStringBuilder CreatePhotoMap(string databasePath, string? clientLibrary);

    (string User, string Password) LoadCredentials();
}

/// <summary>Erzeugt Firebird-Verbindungswerte aus injizierbarer Umgebung.</summary>
public sealed class IbakFdbConnectionOptionsService : IIbakFdbConnectionOptions
{
    private readonly Func<string, string?> _getEnvironmentVariable;

    public IbakFdbConnectionOptionsService(
        Func<string, string?>? getEnvironmentVariable = null)
    {
        _getEnvironmentVariable = getEnvironmentVariable ?? Environment.GetEnvironmentVariable;
    }

    public FbConnectionStringBuilder CreateEmbedded(string databasePath, string charset = "NONE")
    {
        var credentials = LoadCredentials(requireExplicit: false);
        return new FbConnectionStringBuilder
        {
            Database = databasePath,
            UserID = credentials.User,
            Password = credentials.Password,
            ServerType = FbServerType.Embedded,
            Charset = charset
        };
    }

    public FbConnectionStringBuilder CreatePhotoMap(string databasePath, string? clientLibrary)
    {
        var credentials = LoadCredentials(
            requireExplicit: IbakFdbConnectionOptions.IsServerDatabasePath(databasePath));
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

    public (string User, string Password) LoadCredentials() =>
        LoadCredentials(requireExplicit: false);

    private (string User, string Password) LoadCredentials(bool requireExplicit)
    {
        var user = _getEnvironmentVariable(IbakFdbConnectionOptions.UserEnvVar);
        var password = _getEnvironmentVariable(IbakFdbConnectionOptions.PasswordEnvVar);

        if (requireExplicit
            && (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password)))
        {
            throw new InvalidOperationException(
                "Firebird-Serverzugriff benoetigt ausdrueckliche Zugangsdaten. " +
                $"Setze {IbakFdbConnectionOptions.UserEnvVar} und {IbakFdbConnectionOptions.PasswordEnvVar}; " +
                "die lokalen Embedded-Standardwerte werden fuer Serverpfade nicht verwendet.");
        }

        return (
            string.IsNullOrWhiteSpace(user) ? IbakFdbConnectionOptions.DefaultUser : user.Trim(),
            string.IsNullOrWhiteSpace(password) ? IbakFdbConnectionOptions.DefaultPassword : password);
    }
}

/// <summary>Kompatibilitaetsfassade; nur die reine Serverpfadregel bleibt statisch.</summary>
public static class IbakFdbConnectionOptions
{
    public const string UserEnvVar = "IBAK_FDB_USER";
    public const string PasswordEnvVar = "IBAK_FDB_PASSWORD";
    public const string DefaultUser = "SYSDBA";
    public const string DefaultPassword = "masterkey";

    private static readonly IIbakFdbConnectionOptions Default =
        new IbakFdbConnectionOptionsService();

    public static IIbakFdbConnectionOptions Current => Default;

    [Obsolete("Globaler Austausch wurde entfernt. Den Dienst per Konstruktor uebergeben.")]
    public static void Use(IIbakFdbConnectionOptions options) =>
        throw new NotSupportedException(
            "Die globalen IBAK-Verbindungsoptionen koennen nicht mehr ausgetauscht werden. " +
            "IIbakFdbConnectionOptions bitte per Konstruktor uebergeben.");

    public static FbConnectionStringBuilder CreateEmbedded(
        string databasePath,
        string charset = "NONE") =>
        Current.CreateEmbedded(databasePath, charset);

    public static FbConnectionStringBuilder CreatePhotoMap(
        string databasePath,
        string? clientLibrary) =>
        Current.CreatePhotoMap(databasePath, clientLibrary);

    public static (string User, string Password) LoadCredentials() =>
        Current.LoadCredentials();

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
